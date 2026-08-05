using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeTransactions;

public sealed class EmployeeTransactionService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IEmployeeTransactionService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<EmployeeTransactionResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeTransactionFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeTransactionFilterRequest();

        var baseQuery = dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId);

        if (filters.EmployeeId.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.EmployeeId == filters.EmployeeId);
        }

        if (filters.Type.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Type == filters.Type);
        }

        if (filters.TransactionDateFrom.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.TransactionDate >= filters.TransactionDateFrom);
        }

        if (filters.TransactionDateTo.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.TransactionDate <= filters.TransactionDateTo);
        }

        if (filters.IsProcessed.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.IsProcessed == filters.IsProcessed);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(t =>
                t.Employee.Name.Contains(search) ||
                t.Employee.Code.Contains(search) ||
                (t.Notes != null && t.Notes.Contains(search)));
        }

        var query = baseQuery
            .OrderByDescending(t => t.TransactionDate)
            .ThenBy(t => t.Employee.Name)
            .ThenBy(t => t.Id);

        var pageResult = await paginationService.PaginateAsync<
            EmployeeTransaction,
            EmployeeTransactionResponse>(
            query,
            pagination,
            cancellationToken);

        if (pageResult.IsFailure)
        {
            return pageResult;
        }

        return pageResult;
    }

    public async Task<Result<EmployeeTransactionResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if(id <= 0) {
            return Result<EmployeeTransactionResponse>.Failure(
                Error.Validation(
                    "EmployeeTransaction.Id.Invalid",
                    "معرف معاملة الموظف غير صالح."));
        }

        var transaction = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, cancellationToken);

        if (transaction is null)
        {
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "EmployeeTransaction.NotFound",
                    "لم يتم العثور على معاملة الموظف المطلوبة."));
        }
        var employee = await dbContext.Employees.Select(e => new { e.Id, e.Name ,e.CompanyId })
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == transaction.EmployeeId && e.CompanyId == companyId, cancellationToken);
        if (employee is null) {
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "Employee.NotFound",
                    "الموظف المحدد غير موجود."));
        }
        return Result<EmployeeTransactionResponse>.Success(
            MapToResponse(transaction, employee.Name));
    }

    public async Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "Employee.NotFound",
                    "الموظف المحدد غير موجود."));
        }

        var transaction = new EmployeeTransaction
        {
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsProcessed = false
        };

        dbContext.EmployeeTransactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<EmployeeTransactionResponse>.Success(
            MapToResponse(transaction, employee.Name));
    }

    public async Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.EmployeeTransactions
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, cancellationToken);

        if (transaction is null)
        {
            return Result<EmployeeTransactionResponse>.Failure(
                Error.NotFound(
                    "EmployeeTransaction.NotFound",
                    "لم يتم العثور على معاملة الموظف المطلوبة."));
        }
        var employee = await dbContext.Employees.Select(e => new { e.Id, e.Name, e.CompanyId })
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

        if (transaction.EmployeeId != request.EmployeeId&& employee == null)
        {
            return Result<EmployeeTransactionResponse>.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "الموظف المحدد غير موجود."));            
        }

        transaction.Type = request.Type;
        transaction.Amount = request.Amount;
        transaction.TransactionDate = request.TransactionDate;
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        dbContext.EmployeeTransactions.Update(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
  
        return Result<EmployeeTransactionResponse>.Success(
            MapToResponse(transaction, employee.Name));
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, cancellationToken);

        if (transaction is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "EmployeeTransaction.NotFound",
                    "لم يتم العثور على معاملة الموظف المطلوبة."));
        }

        dbContext.EmployeeTransactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static EmployeeTransactionResponse MapToResponse(EmployeeTransaction transaction, string? employeeName = null)
    {
        return new EmployeeTransactionResponse(
            transaction.Id,
            transaction.CompanyId,
            transaction.EmployeeId,
            employeeName ?? transaction.Employee?.Name ?? string.Empty,
            transaction.Type,
            transaction.Amount,
            transaction.TransactionDate,
            transaction.Notes,
            transaction.IsProcessed);
    }
}
