using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeTransactions;

public sealed class EmployeeTransactionService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    ICashVoucherService cashVoucherService,
    TimeProvider timeProvider)
    : IEmployeeTransactionService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    // ─── GET ALL ────────────────────────────────────────────────────────────

    public async Task<Result<PagedResponse<EmployeeTransactionResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeTransactionFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeTransactionFilterRequest();

        var query = dbContext.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.Employee)
            .Include(t => t.CashVoucher)
            .Include(t => t.Cashbox)
            .Where(t => t.CompanyId == companyId);

        if (filters.EmployeeId.HasValue)
            query = query.Where(t => t.EmployeeId == filters.EmployeeId.Value);

        if (filters.Type.HasValue)
            query = query.Where(t => t.Type == filters.Type.Value);

        if (filters.TransactionDateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= filters.TransactionDateFrom.Value);

        if (filters.TransactionDateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= filters.TransactionDateTo.Value);

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Employee.Name.Contains(search) ||
                t.Employee.Code.Contains(search) ||
                (t.Notes != null && t.Notes.Contains(search)) ||
                t.CashVoucher.VoucherNumber.Contains(search));
        }

        var ordered = query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id);

        return await paginationService.PaginateAsync<EmployeeTransaction, EmployeeTransactionResponse>(
            ordered,
            pagination,
            cancellationToken);
    }

    // ─── GET BY ID ──────────────────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.Employee)
            .Include(t => t.CashVoucher)
            .Include(t => t.Cashbox)
            .FirstOrDefaultAsync(
                t => t.Id == id && t.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result<EmployeeTransactionResponse>.Failure(NotFound(id));

        return Result<EmployeeTransactionResponse>.Success(MapToResponse(entry));
    }

    // ─── GET BALANCE ────────────────────────────────────────────────────────

    public async Task<Result<EmployeeAccountBalanceResponse>> GetBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == employeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeAccountBalanceResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var totals = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && t.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Credit = g.Where(t =>
                    t.Type == EmployeeTransactionType.Credit ||
                    t.Type == EmployeeTransactionType.Bonus)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,

                Debit = g.Where(t =>
                    t.Type == EmployeeTransactionType.Debit ||
                    t.Type == EmployeeTransactionType.Deduction ||
                    t.Type == EmployeeTransactionType.Withdrawal ||
                    t.Type == EmployeeTransactionType.Advance)
                    .Sum(t => (decimal?)t.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var credit = totals?.Credit ?? 0m;
        var debit = totals?.Debit ?? 0m;

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                TotalCredit: credit,
                TotalDebit: debit,
                Balance: credit - debit));
    }

    // ─── ADD (single manual entry) ──────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var direction = IsCredit(request.Type)
            ? CashDirection.Receipt
            : CashDirection.Payment;

        var typeLabel = request.Type.ToString();
        var description = $"حركة حساب موظف ({typeLabel}): {employee.Name}";

        var voucherRequest = new CashVoucherBulkVoucherRequest(
            VoucherDate: request.TransactionDate,
            Direction: direction,
            CashboxId: request.CashboxId,
            CashMovementTypeId: request.CashMovementTypeId,
            EmployeeId: employee.Id,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: request.Amount,
            ReferenceNumber: null,
            Description: description,
            Notes: request.Notes,
            ExchangeRate: null);

        var voucherBulkResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: [new CashVoucherBulkAddItemRequest(voucherRequest)]),
            cancellationToken);

        if (voucherBulkResult.IsFailure)
            return Result<EmployeeTransactionResponse>.Failure(voucherBulkResult.Errors);

        var createdVoucher = voucherBulkResult.Value.Items.First().Voucher!;
        var runningBalance = await ComputeNewRunningBalanceAsync(
            request.EmployeeId, request.Type, request.Amount, cancellationToken);

        var entry = new EmployeeTransaction
        {
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Notes = request.Notes?.Trim(),
            RunningBalance = runningBalance,
            SourceType = EmployeeTransactionSource.Manual,
            CashVoucherId = createdVoucher.Id,
            CashBoxId = request.CashboxId
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        entry.Employee = employee;

        return Result<EmployeeTransactionResponse>.Success(
            new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
    }

    // ─── ADD BULK (multiple manual entries) ─────────────────────────────────

    public async Task<Result<List<EmployeeTransactionResponse>>> AddBulkAsync(
        BulkEmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Entries.Count == 0)
            return Result<List<EmployeeTransactionResponse>>.Failure(
                Error.Validation("EmployeeTransaction.EmptyBatch", "يجب إرسال معاملة واحدة على الأقل."));

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime);
        var employeeIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToList();

        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var missingId = employeeIds.FirstOrDefault(id => !employees.ContainsKey(id));
        if (missingId != 0 && !employees.ContainsKey(missingId))
            return Result<List<EmployeeTransactionResponse>>.Failure(
                Error.NotFound("Employee.NotFound", $"الموظف رقم {missingId} غير موجود."));

        var voucherItems = new List<CashVoucherBulkItemRequest>(request.Entries.Count);
        for (int i = 0; i < request.Entries.Count; i++)
        {
            var item = request.Entries[i];
            var cashboxId = item.CashboxId ?? request.DefaultCashboxId;
            var movementTypeId = item.CashMovementTypeId ?? request.DefaultCashMovementTypeId;

            if (!cashboxId.HasValue || !movementTypeId.HasValue)
                return Result<List<EmployeeTransactionResponse>>.Failure(
                    Error.Validation("EmployeeTransaction.CashboxRequired", $"يجب تحديد الصندوق ونوع الحركة للمعاملة رقم {i + 1}."));

            var txDate = item.TransactionDate ?? request.DefaultTransactionDate ?? today;
            var direction = IsCredit(item.Type) ? CashDirection.Receipt : CashDirection.Payment;
            var emp = employees[item.EmployeeId];

            var voucherReq = new CashVoucherBulkVoucherRequest(
                VoucherDate: txDate,
                Direction: direction,
                CashboxId: cashboxId.Value,
                CashMovementTypeId: movementTypeId.Value,
                EmployeeId: emp.Id,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: item.Amount,
                ReferenceNumber: null,
                Description: $"حركة حساب موظف ({item.Type}): {emp.Name}",
                Notes: item.Notes,
                ExchangeRate: null);

            voucherItems.Add(new CashVoucherBulkAddItemRequest(voucherReq));
        }

        var voucherResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: voucherItems),
            cancellationToken);

        if (voucherResult.IsFailure)
            return Result<List<EmployeeTransactionResponse>>.Failure(voucherResult.Errors);

        var voucherResponses = voucherResult.Value.Items;

        var latestBalances = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && employeeIds.Contains(t.EmployeeId))
            .GroupBy(t => t.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                LastBalance = g.OrderByDescending(t => t.TransactionDate)
                               .ThenByDescending(t => t.Id)
                               .Select(t => t.RunningBalance)
                               .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.LastBalance, cancellationToken);

        var runningBalances = new Dictionary<int, decimal>();
        foreach (var id in employeeIds)
        {
            runningBalances[id] = latestBalances.GetValueOrDefault(id, 0m);
        }

        var transactions = new List<EmployeeTransaction>(request.Entries.Count);
        for (int i = 0; i < request.Entries.Count; i++)
        {
            var item = request.Entries[i];
            var txDate = item.TransactionDate ?? request.DefaultTransactionDate ?? today;
            var cashboxId = (item.CashboxId ?? request.DefaultCashboxId)!.Value;
            var createdVoucher = voucherResponses[i].Voucher!;

            var current = runningBalances[item.EmployeeId];
            var updatedBalance = IsCredit(item.Type) ? current + item.Amount : current - item.Amount;
            runningBalances[item.EmployeeId] = updatedBalance;

            var entry = new EmployeeTransaction
            {
                CompanyId = companyId,
                EmployeeId = item.EmployeeId,
                Type = item.Type,
                Amount = item.Amount,
                TransactionDate = txDate,
                Notes = item.Notes?.Trim(),
                RunningBalance = updatedBalance,
                SourceType = EmployeeTransactionSource.Manual,
                CashVoucherId = createdVoucher.Id,
                CashBoxId = cashboxId
            };

            transactions.Add(entry);
        }

        dbContext.EmployeeTransactions.AddRange(transactions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = new List<EmployeeTransactionResponse>(transactions.Count);
        for (int i = 0; i < transactions.Count; i++)
        {
            var entry = transactions[i];
            var emp = employees[entry.EmployeeId];
            var createdVoucher = voucherResponses[i].Voucher!;

            responses.Add(new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: emp.Code,
                EmployeeName: emp.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
        }

        return Result<List<EmployeeTransactionResponse>>.Success(responses);
    }

    // ─── WITHDRAW (single cash withdrawal / advance) ────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> WithdrawAsync(
        EmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Type is not (EmployeeTransactionType.Withdrawal or EmployeeTransactionType.Advance))
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Validation("EmployeeTransaction.InvalidWithdrawalType", "نوع المعاملة يجب أن يكون سحب نقدي أو سلفة."));

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var typeLabel = request.Type == EmployeeTransactionType.Advance ? "سلفة" : "سحب";
        var description = $"{typeLabel} للموظف: {employee.Name}";

        var voucherRequest = new CashVoucherBulkVoucherRequest(
            VoucherDate: request.TransactionDate,
            Direction: CashDirection.Payment,
            CashboxId: request.CashboxId,
            CashMovementTypeId: request.CashMovementTypeId,
            EmployeeId: employee.Id,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: request.Amount,
            ReferenceNumber: null,
            Description: description,
            Notes: request.Notes,
            ExchangeRate: null);

        var voucherBulkResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: [new CashVoucherBulkAddItemRequest(voucherRequest)]),
            cancellationToken);

        if (voucherBulkResult.IsFailure)
            return Result<EmployeeTransactionResponse>.Failure(voucherBulkResult.Errors);

        var createdVoucher = voucherBulkResult.Value.Items.First().Voucher!;
        var currentBalance = await GetCurrentBalanceAsync(request.EmployeeId, cancellationToken);
        var newBalance = currentBalance - request.Amount;

        var entry = new EmployeeTransaction
        {
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Notes = request.Notes?.Trim(),
            RunningBalance = newBalance,
            SourceType = EmployeeTransactionSource.Manual,
            CashVoucherId = createdVoucher.Id,
            CashBoxId = request.CashboxId
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        entry.Employee = employee;

        return Result<EmployeeTransactionResponse>.Success(
            new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
    }

    // ─── WITHDRAW BULK (multiple cash withdrawals / advances) ───────────────

    public async Task<Result<List<EmployeeTransactionResponse>>> WithdrawBulkAsync(
        BulkEmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Entries.Count == 0)
            return Result<List<EmployeeTransactionResponse>>.Failure(
                Error.Validation("EmployeeTransaction.EmptyBatch", "يجب إرسال معاملة سحب واحدة على الأقل."));

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime);
        var employeeIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToList();

        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var missingId = employeeIds.FirstOrDefault(id => !employees.ContainsKey(id));
        if (missingId != 0 && !employees.ContainsKey(missingId))
            return Result<List<EmployeeTransactionResponse>>.Failure(
                Error.NotFound("Employee.NotFound", $"الموظف رقم {missingId} غير موجود."));

        var voucherItems = new List<CashVoucherBulkItemRequest>(request.Entries.Count);
        for (int i = 0; i < request.Entries.Count; i++)
        {
            var item = request.Entries[i];
            var cashboxId = item.CashboxId ?? request.DefaultCashboxId;
            var movementTypeId = item.CashMovementTypeId ?? request.DefaultCashMovementTypeId;
            var txDate = item.TransactionDate ?? request.DefaultTransactionDate ?? today;

            if (!cashboxId.HasValue || !movementTypeId.HasValue)
                return Result<List<EmployeeTransactionResponse>>.Failure(
                    Error.Validation("EmployeeTransaction.CashboxRequired", $"يجب تحديد الصندوق ونوع الحركة للمعاملة رقم {i + 1}."));

            var emp = employees[item.EmployeeId];
            var typeLabel = item.Type == EmployeeTransactionType.Advance ? "سلفة" : "سحب";

            var voucherReq = new CashVoucherBulkVoucherRequest(
                VoucherDate: txDate,
                Direction: CashDirection.Payment,
                CashboxId: cashboxId.Value,
                CashMovementTypeId: movementTypeId.Value,
                EmployeeId: emp.Id,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: item.Amount,
                ReferenceNumber: null,
                Description: $"{typeLabel} للموظف: {emp.Name}",
                Notes: item.Notes,
                ExchangeRate: null);

            voucherItems.Add(new CashVoucherBulkAddItemRequest(voucherReq));
        }

        var voucherResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: voucherItems),
            cancellationToken);

        if (voucherResult.IsFailure)
            return Result<List<EmployeeTransactionResponse>>.Failure(voucherResult.Errors);

        var voucherResponses = voucherResult.Value.Items;

        var latestBalances = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && employeeIds.Contains(t.EmployeeId))
            .GroupBy(t => t.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                LastBalance = g.OrderByDescending(t => t.TransactionDate)
                               .ThenByDescending(t => t.Id)
                               .Select(t => t.RunningBalance)
                               .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.LastBalance, cancellationToken);

        var runningBalances = new Dictionary<int, decimal>();
        foreach (var id in employeeIds)
        {
            runningBalances[id] = latestBalances.GetValueOrDefault(id, 0m);
        }

        var transactions = new List<EmployeeTransaction>(request.Entries.Count);
        for (int i = 0; i < request.Entries.Count; i++)
        {
            var item = request.Entries[i];
            var txDate = item.TransactionDate ?? request.DefaultTransactionDate ?? today;
            var cashboxId = (item.CashboxId ?? request.DefaultCashboxId)!.Value;
            var createdVoucher = voucherResponses[i].Voucher!;

            var current = runningBalances[item.EmployeeId];
            var newBalance = current - item.Amount;
            runningBalances[item.EmployeeId] = newBalance;

            var entry = new EmployeeTransaction
            {
                CompanyId = companyId,
                EmployeeId = item.EmployeeId,
                Type = item.Type,
                Amount = item.Amount,
                TransactionDate = txDate,
                Notes = item.Notes?.Trim(),
                RunningBalance = newBalance,
                SourceType = EmployeeTransactionSource.Manual,
                CashVoucherId = createdVoucher.Id,
                CashBoxId = cashboxId
            };

            transactions.Add(entry);
        }

        dbContext.EmployeeTransactions.AddRange(transactions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = new List<EmployeeTransactionResponse>(transactions.Count);
        for (int i = 0; i < transactions.Count; i++)
        {
            var entry = transactions[i];
            var emp = employees[entry.EmployeeId];
            var createdVoucher = voucherResponses[i].Voucher!;

            responses.Add(new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: emp.Code,
                EmployeeName: emp.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
        }

        return Result<List<EmployeeTransactionResponse>>.Success(responses);
    }

    // ─── POST SALARY CREDIT (internal from PayrollEntryService) ─────────────

    public async Task<Result<EmployeeTransactionResponse>> PostSalaryCreditAsync(
        int employeeId,
        decimal amount,
        int payrollEntryId,
        DateOnly transactionDate,
        int cashboxId,
        int cashMovementTypeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == employeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var voucherRequest = new CashVoucherBulkVoucherRequest(
            VoucherDate: transactionDate,
            Direction: CashDirection.Receipt,
            CashboxId: cashboxId,
            CashMovementTypeId: cashMovementTypeId,
            EmployeeId: employee.Id,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: $"PAYROLL-{payrollEntryId}",
            Description: $"قيد راتب للموظف: {employee.Name}",
            Notes: $"راتب مقيد من مسير الرواتب رقم {payrollEntryId}",
            ExchangeRate: null);

        var voucherBulkResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: [new CashVoucherBulkAddItemRequest(voucherRequest)]),
            cancellationToken);

        if (voucherBulkResult.IsFailure)
            return Result<EmployeeTransactionResponse>.Failure(voucherBulkResult.Errors);

        var createdVoucher = voucherBulkResult.Value.Items.First().Voucher!;
        var currentBalance = await GetCurrentBalanceAsync(employeeId, cancellationToken);
        var newBalance = currentBalance + amount;

        var entry = new EmployeeTransaction
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            Type = EmployeeTransactionType.Credit,
            Amount = amount,
            TransactionDate = transactionDate,
            Notes = $"راتب مقيد من مسير الرواتب رقم {payrollEntryId}",
            RunningBalance = newBalance,
            SourceType = EmployeeTransactionSource.Payroll,
            SourceId = payrollEntryId,
            CashVoucherId = createdVoucher.Id,
            CashBoxId = cashboxId
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        entry.Employee = employee;

        return Result<EmployeeTransactionResponse>.Success(
            new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
    }

    // ─── POST SALARY CREDIT BULK (internal from PayrollEntryService) ─────────

    public async Task<Result<List<EmployeeTransactionResponse>>> PostSalaryCreditBulkAsync(
        IReadOnlyList<EmployeeSalaryCreditItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return Result<List<EmployeeTransactionResponse>>.Success([]);

        var employeeIds = items.Select(i => i.EmployeeId).Distinct().ToList();

        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var voucherItems = new List<CashVoucherBulkItemRequest>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var emp = employees[item.EmployeeId];

            var voucherReq = new CashVoucherBulkVoucherRequest(
                VoucherDate: item.TransactionDate,
                Direction: CashDirection.Receipt,
                CashboxId: item.CashboxId,
                CashMovementTypeId: item.CashMovementTypeId,
                EmployeeId: emp.Id,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: item.Amount,
                ReferenceNumber: $"PAYROLL-{item.PayrollEntryId}",
                Description: $"قيد راتب للموظف: {emp.Name}",
                Notes: item.Notes ?? $"راتب مقيد من مسير الرواتب رقم {item.PayrollEntryId}",
                ExchangeRate: null);

            voucherItems.Add(new CashVoucherBulkAddItemRequest(voucherReq));
        }

        var voucherResult = await cashVoucherService.BulkAsync(
            new CashVoucherBulkRequest(Items: voucherItems),
            cancellationToken);

        if (voucherResult.IsFailure)
            return Result<List<EmployeeTransactionResponse>>.Failure(voucherResult.Errors);

        var voucherResponses = voucherResult.Value.Items;

        var latestTransactions = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && employeeIds.Contains(t.EmployeeId))
            .GroupBy(t => t.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                LastRunningBalance = g.OrderByDescending(t => t.TransactionDate)
                                      .ThenByDescending(t => t.Id)
                                      .Select(t => t.RunningBalance)
                                      .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.LastRunningBalance, cancellationToken);

        var runningBalances = new Dictionary<int, decimal>();
        foreach (var id in employeeIds)
        {
            runningBalances[id] = latestTransactions.GetValueOrDefault(id, 0m);
        }

        var transactions = new List<EmployeeTransaction>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var currentBalance = runningBalances[item.EmployeeId];
            var newBalance = currentBalance + item.Amount;
            runningBalances[item.EmployeeId] = newBalance;

            var createdVoucher = voucherResponses[i].Voucher!;
            var transactionNotes = !string.IsNullOrWhiteSpace(item.Notes)
                ? item.Notes.Trim()
                : $"راتب مقيد من مسير الرواتب رقم {item.PayrollEntryId}";

            var entry = new EmployeeTransaction
            {
                CompanyId = companyId,
                EmployeeId = item.EmployeeId,
                Type = EmployeeTransactionType.Credit,
                Amount = item.Amount,
                TransactionDate = item.TransactionDate,
                Notes = transactionNotes,
                RunningBalance = newBalance,
                SourceType = EmployeeTransactionSource.Payroll,
                SourceId = item.PayrollEntryId,
                CashVoucherId = createdVoucher.Id,
                CashBoxId = item.CashboxId
            };

            transactions.Add(entry);
        }

        dbContext.EmployeeTransactions.AddRange(transactions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = new List<EmployeeTransactionResponse>(transactions.Count);
        for (int i = 0; i < transactions.Count; i++)
        {
            var entry = transactions[i];
            var emp = employees[entry.EmployeeId];
            var createdVoucher = voucherResponses[i].Voucher!;

            responses.Add(new EmployeeTransactionResponse(
                Id: entry.Id,
                CompanyId: entry.CompanyId,
                EmployeeId: entry.EmployeeId,
                EmployeeCode: emp.Code,
                EmployeeName: emp.Name,
                Type: entry.Type,
                Amount: entry.Amount,
                TransactionDate: entry.TransactionDate,
                Notes: entry.Notes,
                RunningBalance: entry.RunningBalance,
                SourceType: entry.SourceType,
                SourceId: entry.SourceId,
                CashVoucherId: entry.CashVoucherId,
                CashVoucherNumber: createdVoucher.VoucherNumber,
                CashBoxId: entry.CashBoxId,
                CashboxName: createdVoucher.CashboxName));
        }

        return Result<List<EmployeeTransactionResponse>>.Success(responses);
    }

    // ─── UPDATE ─────────────────────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeTransactionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.EmployeeTransactions
            .Include(t => t.Employee)
            .Include(t => t.CashVoucher)
            .Include(t => t.Cashbox)
            .FirstOrDefaultAsync(
                t => t.Id == id && t.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result<EmployeeTransactionResponse>.Failure(NotFound(id));

        if (entry.SourceType == EmployeeTransactionSource.Payroll)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.PayrollPosted",
                    "لا يمكن تعديل قيد مقيد تلقائيًا من مسير الرواتب."));

        entry.Amount = request.Amount;
        entry.TransactionDate = request.TransactionDate;
        entry.Notes = request.Notes?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<EmployeeTransactionResponse>.Success(MapToResponse(entry));
    }

    // ─── DELETE ─────────────────────────────────────────────────────────────

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.EmployeeTransactions
            .FirstOrDefaultAsync(
                t => t.Id == id && t.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result.Failure(NotFound(id));

        if (entry.SourceType == EmployeeTransactionSource.Payroll)
            return Result.Failure(
                Error.Conflict(
                    "EmployeeTransaction.PayrollPosted",
                    "لا يمكن حذف قيد مقيد تلقائيًا من مسير الرواتب."));

        dbContext.EmployeeTransactions.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ─── GET STATEMENT (Report) ─────────────────────────────────────────────

    public async Task<Result<EmployeeStatementResponse>> GetStatementAsync(
        int employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (fromDate > toDate)
            return Result<EmployeeStatementResponse>.Failure(
                Error.Validation("EmployeeTransaction.InvalidDateRange", "تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء."));

        var employee = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.CompanyId == companyId, cancellationToken);

        if (employee is null)
            return Result<EmployeeStatementResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var openingBalance = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId &&
                        t.EmployeeId == employeeId &&
                        t.TransactionDate < fromDate)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Select(t => (decimal?)t.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var transactions = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.CashVoucher)
            .Include(t => t.Cashbox)
            .Where(t => t.CompanyId == companyId &&
                        t.EmployeeId == employeeId &&
                        t.TransactionDate >= fromDate &&
                        t.TransactionDate <= toDate)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var items = transactions.Select(t =>
        {
            bool isCredit = IsCredit(t.Type);
            return new EmployeeStatementItem(
                TransactionId: t.Id,
                TransactionDate: t.TransactionDate,
                Type: t.Type,
                Amount: t.Amount,
                Credit: isCredit ? t.Amount : 0m,
                Debit: !isCredit ? t.Amount : 0m,
                RunningBalance: t.RunningBalance,
                SourceType: t.SourceType.ToString(),
                SourceId: t.SourceId,
                CashVoucherId: t.CashVoucherId,
                CashVoucherNumber: t.CashVoucher.VoucherNumber,
                CashBoxId: t.CashBoxId,
                CashboxName: t.Cashbox.Name,
                Notes: t.Notes);
        }).ToList();

        var totalCredit = items.Sum(i => i.Credit);
        var totalDebit = items.Sum(i => i.Debit);
        var totalSalaryCredit = transactions.Where(t => t.SourceType == EmployeeTransactionSource.Payroll).Sum(t => t.Amount);
        var totalCashWithdrawal = transactions.Where(t => t.CashVoucherId > 0).Sum(t => t.Amount);
        var closingBalance = transactions.Count > 0 ? transactions.Last().RunningBalance : openingBalance;

        var summary = new EmployeeStatementSummary(
            EmployeeId: employee.Id,
            EmployeeCode: employee.Code,
            EmployeeName: employee.Name,
            FromDate: fromDate,
            ToDate: toDate,
            OpeningBalance: openingBalance,
            TotalCredit: totalCredit,
            TotalDebit: totalDebit,
            TotalSalaryCredit: totalSalaryCredit,
            TotalCashWithdrawal: totalCashWithdrawal,
            ClosingBalance: closingBalance,
            TotalTransactions: transactions.Count);

        var response = new EmployeeStatementResponse(
            Summary: summary,
            Transactions: items);

        return Result<EmployeeStatementResponse>.Success(response);
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────

    private async Task<decimal> GetCurrentBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && t.EmployeeId == employeeId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Select(t => (decimal?)t.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? 0m;
    }

    private async Task<decimal> ComputeNewRunningBalanceAsync(
        int employeeId,
        EmployeeTransactionType type,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var current = await GetCurrentBalanceAsync(employeeId, cancellationToken);
        return IsCredit(type) ? current + amount : current - amount;
    }

    private static bool IsCredit(EmployeeTransactionType type) =>
        type is EmployeeTransactionType.Credit or EmployeeTransactionType.Bonus;

    private static EmployeeTransactionResponse MapToResponse(EmployeeTransaction t) =>
        new(
            Id: t.Id,
            CompanyId: t.CompanyId,
            EmployeeId: t.EmployeeId,
            EmployeeCode: t.Employee?.Code ?? string.Empty,
            EmployeeName: t.Employee?.Name ?? string.Empty,
            Type: t.Type,
            Amount: t.Amount,
            TransactionDate: t.TransactionDate,
            Notes: t.Notes,
            RunningBalance: t.RunningBalance,
            SourceType: t.SourceType,
            SourceId: t.SourceId,
            CashVoucherId: t.CashVoucherId,
            CashVoucherNumber: t.CashVoucher?.VoucherNumber ?? string.Empty,
            CashBoxId: t.CashBoxId,
            CashboxName: t.Cashbox?.Name ?? string.Empty);

    private static Error NotFound(int id) =>
        Error.NotFound(
            "EmployeeTransaction.NotFound",
            $"لم يتم العثور على معاملة الموظف رقم {id}.");
}
