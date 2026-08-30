using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.EmployeeMovements.EmployeeMovementErrors;

namespace MiniErp.Infrastructure.Services.EmployeeMovements;

public sealed class EmployeeMovementService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider)
    : IEmployeeMovementService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<EmployeeMovementResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeMovementFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeMovementFilterRequest();

        var query = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement => movement.CompanyId == companyId);

        if (filters.EmployeeId.HasValue)
        {
            query = query.Where(m => m.EmployeeId == filters.EmployeeId.Value);
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(m => m.MovementDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(m => m.MovementDate <= filters.ToDate.Value);
        }

        if (filters.Type.HasValue)
        {
            query = query.Where(m => m.Type == filters.Type.Value);
        }

        if (filters.Currency.HasValue)
        {
            query = query.Where(m => m.Currency == filters.Currency.Value);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m =>
                m.Employee.Name.Contains(search) ||
                m.Employee.Code.Contains(search) ||
                (m.Notes != null && m.Notes.Contains(search)) ||
                (m.CashVoucher != null && m.CashVoucher.VoucherNumber.Contains(search)));
        }

        var orderedQuery = query
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id);

        var projectedQuery = orderedQuery.Select(m => new EmployeeMovementResponse(
            m.Id,
            m.CompanyId,
            m.EmployeeId,
            m.Employee.Code,
            m.Employee.Name,
            m.Type,
            m.MovementDate,
            m.Currency,
            m.Type == EmployeeMovementType.Credit || m.Type == EmployeeMovementType.Bonus ? m.Credit : m.Debit,
            m.Debit,
            m.Credit,
            m.ExchangeRate,
            m.BaseDebit,
            m.BaseCredit,
            m.CashVoucherId,
            m.CashVoucher != null ? m.CashVoucher.VoucherNumber : null,
            m.Notes,
            m.CreatedOn));

        return await paginationService.PaginateAsync<EmployeeMovement, EmployeeMovementResponse>(
            orderedQuery,
            pagination,
            cancellationToken);
    }

    public async Task<Result<EmployeeMovementResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<EmployeeMovementResponse>.Failure(InvalidId());
        }

        var response = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.Id == id)
            .Select(m => new EmployeeMovementResponse(
                m.Id,
                m.CompanyId,
                m.EmployeeId,
                m.Employee.Code,
                m.Employee.Name,
                m.Type,
                m.MovementDate,
                m.Currency,
                m.Type == EmployeeMovementType.Credit || m.Type == EmployeeMovementType.Bonus ? m.Credit : m.Debit,
                m.Debit,
                m.Credit,
                m.ExchangeRate,
                m.BaseDebit,
                m.BaseCredit,
                m.CashVoucherId,
                m.CashVoucher != null ? m.CashVoucher.VoucherNumber : null,
                m.Notes,
                m.CreatedOn))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<EmployeeMovementResponse>.Failure(NotFound(id))
            : Result<EmployeeMovementResponse>.Success(response);
    }

    public async Task<Result<EmployeeMovementResponse>> AddAsync(
        EmployeeMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var employee = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeMovementResponse>.Failure(
                EmployeeNotFound(request.EmployeeId));
        }

        if (!employee.IsActive)
        {
            return Result<EmployeeMovementResponse>.Failure(
                EmployeeInactive(request.EmployeeId));
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            request.Currency,
            request.MovementDate,
            request.ExchangeRate,
            cancellationToken);

        if (exchangeRateResult.IsFailure)
        {
            return Result<EmployeeMovementResponse>.Failure(exchangeRateResult.Error);
        }

        CashVoucher? cashVoucher = null;
        if (EmployeeAccountRules.RequiresCashVoucher(request.Type))
        {
            if (!request.CashboxId.HasValue)
            {
                return Result<EmployeeMovementResponse>.Failure(CashboxRequiredForAdvance());
            }

            var cashbox = await dbContext.Cashboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Id == request.CashboxId.Value && c.CompanyId == companyId,
                    cancellationToken);

            if (cashbox is null)
            {
                return Result<EmployeeMovementResponse>.Failure(
                    CashboxNotFound(request.CashboxId.Value));
            }

            if (!cashbox.IsActive)
            {
                return Result<EmployeeMovementResponse>.Failure(
                    CashboxInactive(request.CashboxId.Value));
            }

            var voucherNumber = await EntityIdentifierGenerator
                .GenerateUniqueAsync(
                    dbContext,
                    prefix: "PAY",
                    companyId: companyId,
                    existingIdentifiers: dbContext.CashVouchers
                        .IgnoreQueryFilters()
                        .Where(v => v.CompanyId == companyId)
                        .Select(v => v.VoucherNumber),
                    cancellationToken);

            cashVoucher = new CashVoucher
            {
                CompanyId = companyId,
                VoucherNumber = voucherNumber,
                VoucherDate = request.MovementDate,
                Direction = CashDirection.Payment,
                CashboxId = cashbox.Id,
                PartyType = CashPartyType.Employee,
                EmployeeId = employee.Id,
                Amount = request.Amount,
                Currency = request.Currency,
                Description = request.Notes ?? $"Employee {request.Type}",
                IsPosted = true
            };
            cashVoucher.ApplyExchangeRate(
                exchangeRateResult.Value.ExchangeRateId,
                exchangeRateResult.Value.Rate);
            cashVoucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

            dbContext.CashVouchers.Add(cashVoucher);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var movement = new EmployeeMovement
        {
            CompanyId = companyId,
            EmployeeId = employee.Id,
            MovementDate = request.MovementDate,
            Currency = request.Currency,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CashVoucherId = cashVoucher?.Id
        };

        movement.ApplyAmounts(request.Type, request.Amount);
        movement.ApplyExchangeRate(exchangeRateResult.Value.Rate);

        dbContext.EmployeeMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = new EmployeeMovementResponse(
            movement.Id,
            movement.CompanyId,
            movement.EmployeeId,
            employee.Code,
            employee.Name,
            movement.Type,
            movement.MovementDate,
            movement.Currency,
            request.Amount,
            movement.Debit,
            movement.Credit,
            movement.ExchangeRate,
            movement.BaseDebit,
            movement.BaseCredit,
            movement.CashVoucherId,
            cashVoucher?.VoucherNumber,
            movement.Notes,
            movement.CreatedOn);

        return Result<EmployeeMovementResponse>.Success(response);
    }

    public async Task<Result<List<EmployeeMovementResponse>>> AddBulkAsync(
        BulkEmployeeMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Movements is null || request.Movements.Count == 0)
        {
            return Result<List<EmployeeMovementResponse>>.Failure(
                Error.Validation("EmployeeMovements.EmptyBulk", "يجب إرسال حركة موظف واحدة على الأقل."));
        }

        var employeeIds = request.Movements.Select(m => m.EmployeeId).Distinct().ToList();
        var employees = await dbContext.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        if (employees.Count != employeeIds.Count)
        {
            var missingIds = employeeIds.Where(id => !employees.ContainsKey(id)).ToList();
            return Result<List<EmployeeMovementResponse>>.Failure(
                Error.NotFound("Employees.NotFound", $"بعض الموظفين غير موجودين: {string.Join(", ", missingIds)}"));
        }

        var inactive = employees.Values.FirstOrDefault(e => !e.IsActive);
        if (inactive is not null)
        {
            return Result<List<EmployeeMovementResponse>>.Failure(
                EmployeeInactive(inactive.Id));
        }

        var cashboxIds = request.Movements
            .Where(m => m.CashboxId.HasValue && EmployeeAccountRules.RequiresCashVoucher(m.Type))
            .Select(m => m.CashboxId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, Cashbox> cashboxes = [];
        if (cashboxIds.Count > 0)
        {
            cashboxes = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(c => c.CompanyId == companyId && cashboxIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            if (cashboxes.Count != cashboxIds.Count)
            {
                var missing = cashboxIds.Where(id => !cashboxes.ContainsKey(id)).ToList();
                return Result<List<EmployeeMovementResponse>>.Failure(
                    Error.NotFound("Cashboxes.NotFound", $"بعض الخزائن المحددة غير موجودة: {string.Join(", ", missing)}"));
            }
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var results = new List<EmployeeMovementResponse>();

        foreach (var item in request.Movements)
        {
            var employee = employees[item.EmployeeId];

            var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
                item.Currency,
                item.MovementDate,
                item.ExchangeRate,
                cancellationToken);

            if (exchangeRateResult.IsFailure)
            {
                return Result<List<EmployeeMovementResponse>>.Failure(exchangeRateResult.Error);
            }

            CashVoucher? cashVoucher = null;
            if (EmployeeAccountRules.RequiresCashVoucher(item.Type))
            {
                if (!item.CashboxId.HasValue || !cashboxes.TryGetValue(item.CashboxId.Value, out var cashbox))
                {
                    return Result<List<EmployeeMovementResponse>>.Failure(CashboxRequiredForAdvance());
                }

                var voucherNumber = await EntityIdentifierGenerator
                    .GenerateUniqueAsync(
                        dbContext,
                        prefix: "PAY",
                        companyId: companyId,
                        existingIdentifiers: dbContext.CashVouchers
                            .IgnoreQueryFilters()
                            .Where(v => v.CompanyId == companyId)
                            .Select(v => v.VoucherNumber),
                        cancellationToken);

                cashVoucher = new CashVoucher
                {
                    CompanyId = companyId,
                    VoucherNumber = voucherNumber,
                    VoucherDate = item.MovementDate,
                    Direction = CashDirection.Payment,
                    CashboxId = cashbox.Id,
                    PartyType = CashPartyType.Employee,
                    EmployeeId = employee.Id,
                    Amount = item.Amount,
                    Currency = item.Currency,
                    Description = item.Notes ?? $"Employee {item.Type}",
                    IsPosted = true
                };
                cashVoucher.ApplyExchangeRate(
                    exchangeRateResult.Value.ExchangeRateId,
                    exchangeRateResult.Value.Rate);
                cashVoucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

                dbContext.CashVouchers.Add(cashVoucher);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var movement = new EmployeeMovement
            {
                CompanyId = companyId,
                EmployeeId = employee.Id,
                MovementDate = item.MovementDate,
                Currency = item.Currency,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
                CashVoucherId = cashVoucher?.Id
            };

            movement.ApplyAmounts(item.Type, item.Amount);
            movement.ApplyExchangeRate(exchangeRateResult.Value.Rate);

            dbContext.EmployeeMovements.Add(movement);
            await dbContext.SaveChangesAsync(cancellationToken);

            results.Add(new EmployeeMovementResponse(
                movement.Id,
                movement.CompanyId,
                movement.EmployeeId,
                employee.Code,
                employee.Name,
                movement.Type,
                movement.MovementDate,
                movement.Currency,
                item.Amount,
                movement.Debit,
                movement.Credit,
                movement.ExchangeRate,
                movement.BaseDebit,
                movement.BaseCredit,
                movement.CashVoucherId,
                cashVoucher?.VoucherNumber,
                movement.Notes,
                movement.CreatedOn));
        }

        await transaction.CommitAsync(cancellationToken);
        return Result<List<EmployeeMovementResponse>>.Success(results);
    }
}
