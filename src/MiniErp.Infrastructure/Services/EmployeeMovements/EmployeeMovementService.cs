using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Application.Features.ExchangeRates;
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
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
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
                Id: m.Id,
                CompanyId: m.CompanyId,
                EmployeeId: m.EmployeeId,
                EmployeeCode: m.Employee.Code,
                EmployeeName: m.Employee.Name,
                Type: m.Type,
                MovementDate: m.MovementDate,
                Currency: m.Currency,
                Amount: m.Type == EmployeeMovementType.Credit || m.Type == EmployeeMovementType.Bonus ? m.Credit : m.Debit,
                Debit: m.Debit,
                Credit: m.Credit,
                ExchangeRate: m.ExchangeRate,
                BaseDebit: m.BaseDebit,
                BaseCredit: m.BaseCredit,
                CashVoucherId: m.CashVoucherId,
                CashVoucherNumber: m.CashVoucher != null ? m.CashVoucher.VoucherNumber : null,
                Notes: m.Notes,
                CreatedOn: m.CreatedOn))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<EmployeeMovementResponse>.Failure(NotFound(id))
            : Result<EmployeeMovementResponse>.Success(response);
    }

    public async Task<Result<EmployeeMovementResponse>> AddAsync(
        EmployeeMovementRequest request,
        CancellationToken cancellationToken = default)
    {
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

        var movement = new EmployeeMovement
        {
            CompanyId = companyId,
            EmployeeId = employee.Id,
            MovementDate = request.MovementDate,
            Currency = request.Currency,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        movement.ApplyAmounts(request.Type, request.Amount);
        movement.ApplyExchangeRate(exchangeRateResult.Value.Rate);

        dbContext.EmployeeMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new EmployeeMovementResponse(
            Id: movement.Id,
            CompanyId: movement.CompanyId,
            EmployeeId: movement.EmployeeId,
            EmployeeCode: employee.Code,
            EmployeeName: employee.Name,
            Type: movement.Type,
            MovementDate: movement.MovementDate,
            Currency: movement.Currency,
            Amount: request.Amount,
            Debit: movement.Debit,
            Credit: movement.Credit,
            ExchangeRate: movement.ExchangeRate,
            BaseDebit: movement.BaseDebit,
            BaseCredit: movement.BaseCredit,
            CashVoucherId: null,
            CashVoucherNumber: null,
            Notes: movement.Notes,
            CreatedOn: movement.CreatedOn);

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

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var movements = new List<(EmployeeMovement Movement, Employee Employee, EmployeeMovementRequest Request)>();

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
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<EmployeeMovementResponse>>.Failure(exchangeRateResult.Error);
            }

            var movement = new EmployeeMovement
            {
                CompanyId = companyId,
                EmployeeId = employee.Id,
                MovementDate = item.MovementDate,
                Currency = item.Currency,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
            };
            movement.ApplyAmounts(item.Type, item.Amount);
            movement.ApplyExchangeRate(exchangeRateResult.Value.Rate);

            dbContext.EmployeeMovements.Add(movement);
            movements.Add((movement, employee, item));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var results = movements.Select(tuple => new EmployeeMovementResponse(
            Id: tuple.Movement.Id,
            CompanyId: tuple.Movement.CompanyId,
            EmployeeId: tuple.Movement.EmployeeId,
            EmployeeCode: tuple.Employee.Code,
            EmployeeName: tuple.Employee.Name,
            Type: tuple.Movement.Type,
            MovementDate: tuple.Movement.MovementDate,
            Currency: tuple.Movement.Currency,
            Amount: tuple.Request.Amount,
            Debit: tuple.Movement.Debit,
            Credit: tuple.Movement.Credit,
            ExchangeRate: tuple.Movement.ExchangeRate,
            BaseDebit: tuple.Movement.BaseDebit,
            BaseCredit: tuple.Movement.BaseCredit,
            CashVoucherId: null,
            CashVoucherNumber: null,
            Notes: tuple.Movement.Notes,
            CreatedOn: tuple.Movement.CreatedOn)).ToList();

        return Result<List<EmployeeMovementResponse>>.Success(results);
    }

    public async Task<Result<EmployeeMovementReportResponse>> GetReportAsync(
        EmployeeMovementReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(m => m.CompanyId == companyId);

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(m => m.EmployeeId == request.EmployeeId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(m => m.MovementDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(m => m.MovementDate <= request.ToDate.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(m => m.Type == request.Type.Value);
        }

        if (request.Currency.HasValue)
        {
            query = query.Where(m => m.Currency == request.Currency.Value);
        }

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m =>
                m.Employee.Name.Contains(search) ||
                m.Employee.Code.Contains(search) ||
                (m.Notes != null && m.Notes.Contains(search)) ||
                (m.CashVoucher != null && m.CashVoucher.VoucherNumber.Contains(search)));
        }

        var movements = await query
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.EmployeeId,
                EmployeeCode = m.Employee.Code,
                EmployeeName = m.Employee.Name,
                m.MovementDate,
                m.Type,
                m.Currency,
                m.Debit,
                m.Credit,
                m.ExchangeRate,
                m.BaseDebit,
                m.BaseCredit,
                m.Notes,
                m.CashVoucherId,
                CashVoucherNumber = m.CashVoucher != null ? m.CashVoucher.VoucherNumber : null,
                CashVoucherReference = m.CashVoucher != null ? m.CashVoucher.ReferenceNumber : null
            })
            .ToListAsync(cancellationToken);

        decimal runningBalance = 0m;
        var items = new List<EmployeeMovementReportItemResponse>(movements.Count);

        decimal totalDebits = 0m;
        decimal totalCredits = 0m;
        decimal totalBonuses = 0m;
        decimal totalDeductions = 0m;

        foreach (var m in movements)
        {
            var isCredit = EmployeeAccountRules.IsCreditMovement(m.Type);
            var originalAmount = isCredit ? m.Credit : m.Debit;
            var egpAmount = isCredit ? m.BaseCredit : m.BaseDebit;

            totalDebits += m.Debit;
            totalCredits += m.Credit;

            if (m.Type == EmployeeMovementType.Deduction)
            {
                totalDeductions += m.Debit;
            }
            else if (m.Type == EmployeeMovementType.Bonus)
            {
                totalBonuses += m.Credit;
            }

            runningBalance += EmployeeAccountRules.SignedAmount(m.Debit, m.Credit);

            items.Add(new EmployeeMovementReportItemResponse(
                Id: m.Id,
                EmployeeId: m.EmployeeId,
                EmployeeCode: m.EmployeeCode,
                EmployeeName: m.EmployeeName,
                Date: m.MovementDate,
                MovementType: m.Type,
                MovementTypeName: EmployeeAccountRules.GetMovementTypeName(m.Type),
                OriginalAmount: originalAmount,
                Currency: m.Currency,
                ExchangeRate: m.ExchangeRate,
                EgpAmount: egpAmount,
                Debit: m.Debit,
                Credit: m.Credit,
                RunningBalance: runningBalance,
                Reason: m.Notes,
                Notes: m.Notes,
                CashVoucherId: m.CashVoucherId,
                CashVoucherNumber: m.CashVoucherNumber,
                CashVoucherReference: m.CashVoucherReference));
        }

        var summary = new EmployeeMovementReportSummaryResponse(
            TotalDebits: totalDebits,
            TotalCredits: totalCredits,
            NetBalance: EmployeeAccountRules.CalculateBalance(totalCredits, totalDebits),
            TotalBonuses: totalBonuses,
            TotalDeductions: totalDeductions,
            TotalMovements: items.Count);

        return Result<EmployeeMovementReportResponse>.Success(
            new EmployeeMovementReportResponse(
                Summary: summary,
                Items: items));
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var movement = await dbContext.EmployeeMovements
            .FirstOrDefaultAsync(
                m => m.Id == id && m.CompanyId == companyId,
                cancellationToken);

        if (movement is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(NotFound(id));
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                movement.MovementDate,
                nameof(EmployeeMovement.MovementDate),
                cancellationToken);

            if (fiscalYearResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        // Rule 8: Do not allow deleting an EmployeeMovement if it has another financial dependency
        if (movement.CashVoucherId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(LinkedToCashVoucher());
        }

        // Reversal of the financial effect using EmployeeAccountRules:
        // Credit/Bonus -> reverses credit (account balance decreases)
        // Debit/Deduction -> reverses debit (account balance increases)
        // Since employee balance is derived from active non-deleted rows,
        // deleting the entity atomically removes its debit/credit from the ledger.
        _ = EmployeeAccountRules.GetReversalSignedAmount(movement.Type, movement.Debit, movement.Credit);

        dbContext.EmployeeMovements.Remove(movement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
