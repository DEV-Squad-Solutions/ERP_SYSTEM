using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeTransactions;

public sealed class EmployeeTransactionService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
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
            .Where(t => t.CompanyId == companyId);

        if (filters.EmployeeId.HasValue)
            query = query.Where(t => t.EmployeeId == filters.EmployeeId.Value);

        if (filters.Type.HasValue)
            query = query.Where(t => t.Type == filters.Type.Value);

        if (filters.TransactionDateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= filters.TransactionDateFrom.Value);

        if (filters.TransactionDateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= filters.TransactionDateTo.Value);

        if (filters.IsProcessed.HasValue)
        {
            // IsProcessed mapped: any cash-backed type (Withdrawal/Advance) with CashVoucherId set
            var hasCash = filters.IsProcessed.Value;
            query = query.Where(t => hasCash
                ? t.CashVoucherId != null
                : t.CashVoucherId == null);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t =>
                t.Employee.Name.Contains(search) ||
                t.Employee.Code.Contains(search) ||
                (t.Notes != null && t.Notes.Contains(search)));

        var ordered = query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id);

        return await paginationService.PaginateAsync<
            EmployeeTransaction,
            EmployeeTransactionResponse>(
            ordered,
            pagination,
            cancellationToken);
    }

    // ─── GET BY ID ──────────────────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Validation(
                    "EmployeeTransaction.InvalidId",
                    "معرف المعاملة غير صالح."));

        var t = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CompanyId == companyId,
                cancellationToken);

        return t is null
            ? Result<EmployeeTransactionResponse>.Failure(NotFound(id))
            : Result<EmployeeTransactionResponse>.Success(MapToResponse(t));
    }

    // ─── GET BALANCE ────────────────────────────────────────────────────────

    public async Task<Result<EmployeeAccountBalanceResponse>> GetBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Id == employeeId)
            .Select(e => new { e.Id, e.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
            return Result<EmployeeAccountBalanceResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var totals = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && t.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCredit = g
                    .Where(t => t.Type == EmployeeTransactionType.Credit
                             || t.Type == EmployeeTransactionType.Bonus)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                TotalDebit = g
                    .Where(t => t.Type == EmployeeTransactionType.Debit
                             || t.Type == EmployeeTransactionType.Deduction
                             || t.Type == EmployeeTransactionType.Withdrawal
                             || t.Type == EmployeeTransactionType.Advance)
                    .Sum(t => (decimal?)t.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var credit = totals?.TotalCredit ?? 0m;
        var debit  = totals?.TotalDebit  ?? 0m;

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                employee.Id,
                employee.Name,
                credit,
                debit,
                credit - debit));
    }

    // ─── ADD (manual entry) ─────────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        // Cash-backed types must go through WithdrawAsync
        if (request.Type is EmployeeTransactionType.Withdrawal or EmployeeTransactionType.Advance)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Validation(
                    "EmployeeTransaction.UseCashWithdrawal",
                    "لصرف نقدي استخدم نقطة النهاية المخصصة للسحب النقدي.",
                    nameof(request.Type)));

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

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
            SourceType = EmployeeTransactionSource.Manual
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        entry.Employee = employee;
        return Result<EmployeeTransactionResponse>.Success(MapToResponse(entry));
    }

    // ─── WITHDRAW (cash-backed) ─────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> WithdrawAsync(
        EmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Type is not (EmployeeTransactionType.Withdrawal or EmployeeTransactionType.Advance))
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Validation(
                    "EmployeeTransaction.InvalidWithdrawalType",
                    "نوع المعاملة يجب أن يكون سحب نقدي أو سلفة.",
                    nameof(request.Type)));

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));
        }

        // Validate sufficient balance
        var currentBalance = await GetCurrentBalanceAsync(
            request.EmployeeId, cancellationToken);

        if (request.Amount > currentBalance)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.InsufficientBalance",
                    $"الرصيد الحالي للموظف ({currentBalance:N2}) لا يكفي لهذا السحب ({request.Amount:N2})."));
        }

        // Validate cashbox
        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                c => c.CompanyId == companyId && c.Id == request.CashboxId,
                cancellationToken);

        if (cashbox is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "EmployeeTransaction.CashboxNotFound",
                    $"لم يتم العثور على صندوق النقدية رقم {request.CashboxId}.",
                    nameof(request.CashboxId)));
        }
        if (!cashbox.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.CashboxInactive",
                    "الصندوق النقدي المحدد غير نشط.",
                    nameof(request.CashboxId)));
        }

        // Validate movement type must be Payment
        var movementType = await dbContext.CashMovementTypes
            .FirstOrDefaultAsync(
                m => m.CompanyId == companyId && m.Id == request.CashMovementTypeId,
                cancellationToken);

        if (movementType is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "EmployeeTransaction.MovementTypeNotFound",
                    $"لم يتم العثور على نوع الحركة النقدية رقم {request.CashMovementTypeId}.",
                    nameof(request.CashMovementTypeId)));
        }
        if (!movementType.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.MovementTypeInactive",
                    "نوع الحركة النقدية المحدد غير نشط.",
                    nameof(request.CashMovementTypeId)));
        }
        if (movementType.Direction != CashDirection.Payment)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.MovementTypeMustBePayment",
                    "يجب أن يكون نوع الحركة من نوع صرف (دفع).",
                    nameof(request.CashMovementTypeId)));
        }

        // Create CashVoucher
        var typeLabel = request.Type == EmployeeTransactionType.Advance ? "سلفة" : "سحب";
        var voucherNumber = $"EMP-{typeLabel.ToUpperInvariant()}-{employee.Id}-{request.TransactionDate:yyyyMMdd}";

        var voucher = new CashVoucher
        {
            CompanyId = companyId,
            VoucherNumber = voucherNumber,
            VoucherDate = request.TransactionDate,
            Direction = CashDirection.Payment,
            CashboxId = request.CashboxId,
            CashMovementTypeId = request.CashMovementTypeId,
            PartyType = CashPartyType.Employee,
            EmployeeId = employee.Id,
            Amount = request.Amount,
            Currency = cashbox.Currency,
            IsPosted = true,
            Description = $"{typeLabel} للموظف: {employee.Name}",
            Notes = request.Notes
        };
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken); // get voucher.Id

        // Debit the employee account
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
            CashVoucherId = voucher.Id
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        entry.Employee = employee;
        return Result<EmployeeTransactionResponse>.Success(MapToResponse(entry));
    }

    // ─── POST SALARY CREDIT (called by PayrollEntryService) ─────────────────

    public async Task<Result<EmployeeTransactionResponse>> PostSalaryCreditAsync(
        int employeeId,
        decimal amount,
        int payrollEntryId,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == employeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var runningBalance = await ComputeNewRunningBalanceAsync(
            employeeId, EmployeeTransactionType.Credit, amount, cancellationToken);

        var entry = new EmployeeTransaction
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            Type = EmployeeTransactionType.Credit,
            Amount = amount,
            TransactionDate = transactionDate,
            Notes = $"راتب مقيد من مسير الرواتب رقم {payrollEntryId}",
            RunningBalance = runningBalance,
            SourceType = EmployeeTransactionSource.Payroll,
            SourceId = payrollEntryId
        };

        dbContext.EmployeeTransactions.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        entry.Employee = employee;
        return Result<EmployeeTransactionResponse>.Success(MapToResponse(entry));
    }

    // ─── UPDATE ─────────────────────────────────────────────────────────────

    public async Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.EmployeeTransactions
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(
                t => t.Id == id && t.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result<EmployeeTransactionResponse>.Failure(NotFound(id));

        if (entry.CashVoucherId.HasValue)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.LinkedToVoucher",
                    "لا يمكن تعديل معاملة مرتبطة بسند صرف نقدي."));

        if (entry.SourceType == EmployeeTransactionSource.Payroll)
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Conflict(
                    "EmployeeTransaction.PayrollPosted",
                    "لا يمكن تعديل قيد مقيد تلقائيًا من مسير الرواتب."));

        entry.Type = request.Type;
        entry.Amount = request.Amount;
        entry.TransactionDate = request.TransactionDate;
        entry.Notes = request.Notes?.Trim();

        // Recompute running balance is complex without recalculating all subsequent entries.
        // For now we do NOT recalculate here — the balance on this record will be stale.
        // A future migration or background job can recalculate.

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

        if (entry.CashVoucherId.HasValue)
            return Result.Failure(
                Error.Conflict(
                    "EmployeeTransaction.LinkedToVoucher",
                    "لا يمكن حذف معاملة مرتبطة بسند صرف نقدي."));

        if (entry.SourceType == EmployeeTransactionSource.Payroll)
            return Result.Failure(
                Error.Conflict(
                    "EmployeeTransaction.PayrollPosted",
                    "لا يمكن حذف قيد مقيد تلقائيًا من مسير الرواتب."));

        dbContext.EmployeeTransactions.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
            t.Id,
            t.CompanyId,
            t.EmployeeId,
            t.Employee?.Name ?? string.Empty,
            t.Type,
            t.Amount,
            t.TransactionDate,
            t.Notes,
            t.RunningBalance,
            t.SourceType,
            t.SourceId,
            t.CashVoucherId);

    private static Error NotFound(int id) =>
        Error.NotFound(
            "EmployeeTransaction.NotFound",
            $"لم يتم العثور على معاملة الموظف رقم {id}.");
}
