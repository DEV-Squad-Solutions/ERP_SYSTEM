using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FinancialStatementLines;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.FinancialStatementLines.FinancialStatementLineErrors;

namespace MiniErp.Infrastructure.Services.FinancialStatementLines;

public sealed class FinancialStatementLineService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IFinancialStatementLineService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<FinancialStatementLineResponse>>> GetAllAsync(
        PaginationRequest pagination,
        FinancialStatementLineFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateScopeAsync(
            filters.FiscalYearId,
            filters.StatementType,
            requireOpen: false,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<PagedResponse<FinancialStatementLineResponse>>.Failure(
                scopeValidation.Errors);
        }

        var search = filters.Search?.Trim();
        var query = dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == filters.FiscalYearId &&
                line.StatementType == filters.StatementType)
            .Where(line =>
                string.IsNullOrEmpty(search) ||
                line.Code.Contains(search) ||
                line.Name.Contains(search))
            .Where(line =>
                !filters.ParentLineId.HasValue ||
                line.ParentLineId == filters.ParentLineId.Value)
            .Where(line =>
                !filters.IsAssignable.HasValue ||
                line.IsAssignable == filters.IsAssignable.Value)
            .Where(line =>
                !filters.IsActive.HasValue ||
                line.IsActive == filters.IsActive.Value)
            .OrderBy(line => line.DisplayOrder)
            .ThenBy(line => line.Code)
            .ThenBy(line => line.Id);

        return await paginationService.PaginateAsync<
            FinancialStatementLine,
            FinancialStatementLineResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<FinancialStatementLineTreeResponse>>>
        GetTreeAsync(
            int fiscalYearId,
            FinancialStatementType statementType,
            CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateScopeAsync(
            fiscalYearId,
            statementType,
            requireOpen: false,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<IReadOnlyList<FinancialStatementLineTreeResponse>>
                .Failure(scopeValidation.Errors);
        }

        var rows = await dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statementType)
            .OrderBy(line => line.DisplayOrder)
            .ThenBy(line => line.Code)
            .ThenBy(line => line.Id)
            .Select(line => new FinancialStatementLineTreeRow
            {
                Id = line.Id,
                FiscalYearId = line.FiscalYearId,
                StatementType = line.StatementType,
                Code = line.Code,
                Name = line.Name,
                ParentLineId = line.ParentLineId,
                DisplayOrder = line.DisplayOrder,
                IsAssignable = line.IsAssignable,
                IsActive = line.IsActive,
                RowVersion = line.RowVersion
            })
            .ToListAsync(cancellationToken);

        var childrenByParent = rows.ToLookup(row => row.ParentLineId);
        IReadOnlyList<FinancialStatementLineTreeResponse> response = BuildTree(
            parentLineId: null,
            childrenByParent);
        return Result<IReadOnlyList<FinancialStatementLineTreeResponse>>.Success(
            response);
    }

    public async Task<Result<IReadOnlyList<FinancialStatementLineSelectResponse>>>
        GetSelectAsync(
            int fiscalYearId,
            FinancialStatementType statementType,
            CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateScopeAsync(
            fiscalYearId,
            statementType,
            requireOpen: false,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<IReadOnlyList<FinancialStatementLineSelectResponse>>
                .Failure(scopeValidation.Errors);
        }

        var rows = await dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statementType &&
                line.IsActive &&
                line.IsAssignable)
            .OrderBy(line => line.DisplayOrder)
            .ThenBy(line => line.Code)
            .ThenBy(line => line.Id)
            .Select(line => new
            {
                line.Id,
                line.Code,
                line.Name,
                line.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<FinancialStatementLineSelectResponse> response = rows
            .Select(row => new FinancialStatementLineSelectResponse(
                Id: row.Id,
                Code: row.Code,
                Name: row.Name,
                DisplayOrder: row.DisplayOrder))
            .ToList();
        return Result<IReadOnlyList<FinancialStatementLineSelectResponse>>.Success(
            response);
    }

    public async Task<Result<FinancialStatementLineResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FinancialStatementLineResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);
        return response is null
            ? Result<FinancialStatementLineResponse>.Failure(NotFound(id))
            : Result<FinancialStatementLineResponse>.Success(response);
    }

    public async Task<Result<FinancialStatementLineResponse>> AddAsync(
        FinancialStatementLineRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var scopeValidation = await ValidateScopeAsync(
            request.FiscalYearId,
            request.StatementType,
            requireOpen: true,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                scopeValidation.Errors);
        }

        var normalizedCode = request.Code.Trim();
        if (await CodeExistsAsync(
                request.FiscalYearId,
                request.StatementType,
                normalizedCode,
                excludedId: null,
                cancellationToken))
        {
            return Result<FinancialStatementLineResponse>.Failure(
                CodeExists(normalizedCode));
        }

        var parentValidation = await ValidateParentAsync(
            request.FiscalYearId,
            request.StatementType,
            request.ParentLineId,
            lineId: null,
            cancellationToken);
        if (parentValidation.IsFailure)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                parentValidation.Errors);
        }

        var line = request.Adapt<FinancialStatementLine>();
        line.CompanyId = companyId;
        dbContext.FinancialStatementLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(line.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<FinancialStatementLineResponse>.Success(response);
    }

    public async Task<Result<FinancialStatementLineResponse>> UpdateAsync(
        int id,
        FinancialStatementLineUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FinancialStatementLineResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<FinancialStatementLineResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var line = await dbContext.FinancialStatementLines.FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId && entity.Id == id,
            cancellationToken);
        if (line is null)
        {
            return Result<FinancialStatementLineResponse>.Failure(NotFound(id));
        }

        if (!line.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<FinancialStatementLineResponse>.Failure(Concurrency());
        }

        var oldScopeValidation = await ValidateScopeAsync(
            line.FiscalYearId,
            line.StatementType,
            requireOpen: true,
            cancellationToken);
        if (oldScopeValidation.IsFailure)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                oldScopeValidation.Errors);
        }

        var targetScopeValidation = await ValidateScopeAsync(
            request.FiscalYearId,
            request.StatementType,
            requireOpen: true,
            cancellationToken);
        if (targetScopeValidation.IsFailure)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                targetScopeValidation.Errors);
        }

        var hasChildren = await dbContext.FinancialStatementLines.AnyAsync(
            child => child.CompanyId == companyId && child.ParentLineId == id,
            cancellationToken);
        var hasMappings = await dbContext.AccountStatementMappings.AnyAsync(
            mapping =>
                mapping.CompanyId == companyId &&
                mapping.FinancialStatementLineId == id,
            cancellationToken);
        var scopeChanged = line.FiscalYearId != request.FiscalYearId ||
            line.StatementType != request.StatementType;
        if (scopeChanged && hasChildren)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                ParentScopeCannotChange());
        }

        if (scopeChanged && hasMappings)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                MappedLineCannotChangeScope());
        }

        var normalizedCode = request.Code.Trim();
        if (await CodeExistsAsync(
                request.FiscalYearId,
                request.StatementType,
                normalizedCode,
                id,
                cancellationToken))
        {
            return Result<FinancialStatementLineResponse>.Failure(
                CodeExists(normalizedCode));
        }

        var parentValidation = await ValidateParentAsync(
            request.FiscalYearId,
            request.StatementType,
            request.ParentLineId,
            id,
            cancellationToken);
        if (parentValidation.IsFailure)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                parentValidation.Errors);
        }

        if (request.IsAssignable && hasChildren)
        {
            return Result<FinancialStatementLineResponse>.Failure(
                AssignableLineHasChildren());
        }

        if (!request.IsActive && await dbContext.FinancialStatementLines.AnyAsync(
                child =>
                    child.CompanyId == companyId &&
                    child.ParentLineId == id &&
                    child.IsActive,
                cancellationToken))
        {
            return Result<FinancialStatementLineResponse>.Failure(
                InactiveLineHasChildren());
        }

        if (hasMappings && (!request.IsActive || !request.IsAssignable))
        {
            return Result<FinancialStatementLineResponse>.Failure(
                MappedLineCannotBeDisabled());
        }

        var entry = dbContext.Entry(line);
        entry.Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
        request.Adapt(line);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<FinancialStatementLineResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<FinancialStatementLineResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var line = await dbContext.FinancialStatementLines.FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId && entity.Id == id,
            cancellationToken);
        if (line is null)
        {
            return Result.Failure(NotFound(id));
        }

        var scopeValidation = await ValidateScopeAsync(
            line.FiscalYearId,
            line.StatementType,
            requireOpen: true,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result.Failure(scopeValidation.Errors);
        }

        if (await dbContext.FinancialStatementLines.AnyAsync(
                child => child.CompanyId == companyId && child.ParentLineId == id,
                cancellationToken))
        {
            return Result.Failure(HasChildren());
        }

        if (await dbContext.AccountStatementMappings.AnyAsync(
                mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FinancialStatementLineId == id,
                cancellationToken))
        {
            return Result.Failure(HasMappings());
        }

        dbContext.FinancialStatementLines.Remove(line);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<FinancialStatementLineResponse> ProjectResponseQuery(int id) =>
        dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId && line.Id == id)
            .ProjectToType<FinancialStatementLineResponse>();

    private async Task<Result> ValidateScopeAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        bool requireOpen,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(statementType))
        {
            return Result.Failure(InvalidStatementType(statementType));
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year => year.CompanyId == companyId && year.Id == fiscalYearId)
            .Select(year => new { year.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(FiscalYearNotFound(fiscalYearId));
        }

        return requireOpen && fiscalYear.Status == FiscalYearStatus.Closed
            ? Result.Failure(FiscalYearClosed())
            : Result.Success();
    }

    private Task<bool> CodeExistsAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        string code,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FinancialStatementLines.AsNoTracking().AnyAsync(
            line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statementType &&
                (!excludedId.HasValue || line.Id != excludedId.Value) &&
                line.Code.ToUpper() == code.ToUpper(),
            cancellationToken);

    private async Task<Result> ValidateParentAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        int? parentLineId,
        int? lineId,
        CancellationToken cancellationToken)
    {
        if (!parentLineId.HasValue)
        {
            return Result.Success();
        }

        if (lineId == parentLineId)
        {
            return Result.Failure(ParentCannotBeSelf());
        }

        var parent = await dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statementType &&
                line.Id == parentLineId.Value)
            .Select(line => new
            {
                line.Id,
                line.IsActive,
                line.IsAssignable
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (parent is null)
        {
            return Result.Failure(ParentNotFound(parentLineId.Value));
        }

        if (!parent.IsActive)
        {
            return Result.Failure(ParentInactive());
        }

        if (parent.IsAssignable)
        {
            return Result.Failure(ParentMustBeGroup());
        }

        if (lineId.HasValue && await CreatesCycleAsync(
                lineId.Value,
                parent.Id,
                cancellationToken))
        {
            return Result.Failure(HierarchyCycle());
        }

        return Result.Success();
    }

    private async Task<bool> CreatesCycleAsync(
        int lineId,
        int parentLineId,
        CancellationToken cancellationToken)
    {
        var parents = await dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId)
            .ToDictionaryAsync(
                line => line.Id,
                line => line.ParentLineId,
                cancellationToken);

        int? currentId = parentLineId;
        var visited = new HashSet<int>();
        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            if (currentId.Value == lineId)
            {
                return true;
            }

            currentId = parents.GetValueOrDefault(currentId.Value);
        }

        return false;
    }

    private static IReadOnlyList<FinancialStatementLineTreeResponse> BuildTree(
        int? parentLineId,
        ILookup<int?, FinancialStatementLineTreeRow> childrenByParent) =>
        childrenByParent[parentLineId]
            .Select(row => new FinancialStatementLineTreeResponse(
                Id: row.Id,
                FiscalYearId: row.FiscalYearId,
                StatementType: row.StatementType,
                Code: row.Code,
                Name: row.Name,
                ParentLineId: row.ParentLineId,
                DisplayOrder: row.DisplayOrder,
                IsAssignable: row.IsAssignable,
                IsActive: row.IsActive,
                RowVersion: row.RowVersion,
                Children: BuildTree(row.Id, childrenByParent)))
            .ToList();

    private sealed class FinancialStatementLineTreeRow
    {
        public int Id { get; init; }

        public int FiscalYearId { get; init; }

        public FinancialStatementType StatementType { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public int? ParentLineId { get; init; }

        public int DisplayOrder { get; init; }

        public bool IsAssignable { get; init; }

        public bool IsActive { get; init; }

        public byte[] RowVersion { get; init; } = [];
    }
}
