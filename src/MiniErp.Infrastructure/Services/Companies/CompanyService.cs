using System.Data;
using static MiniErp.Application.Features.Companies.CompanyErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Companies;

public sealed class CompanyService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentUserService currentUserService)
    : ICompanyService, IScopedService
{
    public async Task<Result<PagedResponse<CompanyResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CompanyFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CompanyFilterRequest();
        var query = dbContext.Companies
            .AsNoTracking()
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                company.Name.Contains(filters.Search.Trim()) ||
                company.Address.Contains(filters.Search.Trim()) ||
                company.CommercialRegister.Contains(filters.Search.Trim()) ||
                company.TaxNumber.Contains(filters.Search.Trim()) ||
                company.ManagerName.Contains(filters.Search.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                company.Name.Contains(filters.Name.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Address) ||
                company.Address.Contains(filters.Address.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.CommercialRegister) ||
                company.CommercialRegister.Contains(filters.CommercialRegister.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.TaxNumber) ||
                company.TaxNumber.Contains(filters.TaxNumber.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.ManagerName) ||
                company.ManagerName.Contains(filters.ManagerName.Trim()))
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id);

        return await paginationService.PaginateAsync<Company, CompanyResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<CompanyResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CompanyResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == id)
            .ProjectToType<CompanyResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CompanyResponse>.Failure(NotFound(id))
            : Result<CompanyResponse>.Success(response);
    }

    public async Task<Result<CompanyResponse>> AddAsync(
        CompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = request.Adapt<Company>();
        var duplicateErrors = await FindDuplicateAsync(
            company.CommercialRegister,
            company.TaxNumber,
            excludedId: null,
            cancellationToken);

        if (duplicateErrors.Count > 0)
        {
            return Result<CompanyResponse>.Failure(duplicateErrors);
        }

        var currentUserResult = currentUserService.GetUserId();
        if (currentUserResult.IsFailure)
        {
            return Result<CompanyResponse>.Failure(currentUserResult.Error);
        }

        dbContext.Companies.Add(company);
        company.Settings = new CompanySettings
        {
            BaseCurrency = request.BaseCurrency ?? CurrencyCode.EGP,
            StockBalanceCheckMode = request.StockBalanceCheckMode ??
                StockBalanceCheckMode.None
        };

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CompanyResponse>.Success(company.Adapt<CompanyResponse>());
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(
        int id,
        CompanyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CompanyResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CompanyResponse>.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var company = await dbContext.Companies.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (company is null)
        {
            return Result<CompanyResponse>.Failure(NotFound(id));
        }

        if (!company.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CompanyResponse>.Failure(Concurrency());
        }

        var normalizedCompany = request.Adapt<Company>();
        var duplicateErrors = await FindDuplicateAsync(
            normalizedCompany.CommercialRegister,
            normalizedCompany.TaxNumber,
            id,
            cancellationToken);

        if (duplicateErrors.Count > 0)
        {
            return Result<CompanyResponse>.Failure(duplicateErrors);
        }

        var entry = dbContext.Entry(company);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;

        var settings = await dbContext.CompanySettings
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == id,
                cancellationToken);

        var currentBaseCurrency = settings?.BaseCurrency ?? CurrencyCode.EGP;
        if (request.BaseCurrency.HasValue &&
            request.BaseCurrency.Value != currentBaseCurrency &&
            await HasFinancialOrInventoryHistoryAsync(
                id,
                cancellationToken))
        {
            return Result<CompanyResponse>.Failure(BaseCurrencyLocked());
        }

        request.Adapt(company);
        company.UpdatedOn = DateTime.UtcNow;
        entry.Property(entity => entity.UpdatedOn).IsModified = true;

        if (settings is null)
        {
            settings = new CompanySettings
            {
                CompanyId = id,
                BaseCurrency = request.BaseCurrency ?? CurrencyCode.EGP,
                StockBalanceCheckMode = request.StockBalanceCheckMode ??
                    StockBalanceCheckMode.None
            };
            dbContext.CompanySettings.Add(settings);
        }
        else
        {
            if (request.StockBalanceCheckMode.HasValue)
            {
                settings.StockBalanceCheckMode =
                    request.StockBalanceCheckMode.Value;
            }

            if (request.BaseCurrency.HasValue &&
                request.BaseCurrency.Value != settings.BaseCurrency)
            {
                settings.BaseCurrency = request.BaseCurrency.Value;
            }
        }

        company.Settings = settings;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CompanyResponse>.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<CompanyResponse>.Success(company.Adapt<CompanyResponse>());
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
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var company = await dbContext.Companies.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (company is null)
        {
            return Result.Failure(NotFound(id));
        }

        var dependency = await FindCompanyDependencyAsync(
            id,
            cancellationToken);
        if (dependency is not null)
        {
            return Result.Failure(HasDependencies(dependency));
        }

        dbContext.Companies.Remove(company);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<IReadOnlyList<Error>> FindDuplicateAsync(
        string commercialRegister,
        string taxNumber,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var errors = new List<Error>();
        var commercialRegisterExists = await dbContext.Companies.AnyAsync(
            company =>
                (!excludedId.HasValue || company.Id != excludedId.Value) &&
                company.CommercialRegister == commercialRegister,
            cancellationToken);

        if (commercialRegisterExists)
        {
            errors.Add(CommercialRegisterExists(commercialRegister));
        }

        var taxNumberExists = await dbContext.Companies.AnyAsync(
            company =>
                (!excludedId.HasValue || company.Id != excludedId.Value) &&
                company.TaxNumber == taxNumber,
            cancellationToken);

        if (taxNumberExists)
        {
            errors.Add(TaxNumberExists(taxNumber));
        }

        return errors;
    }

    private async Task<bool> HasFinancialOrInventoryHistoryAsync(
        int targetCompanyId,
        CancellationToken cancellationToken) =>
        await dbContext.ExchangeRates
            .IgnoreQueryFilters()
            .AnyAsync(rate => rate.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(invoice => invoice.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.InvoiceLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.InvoicePayments
            .IgnoreQueryFilters()
            .AnyAsync(payment => payment.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .AnyAsync(cashbox => cashbox.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(voucher => voucher.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.BusinessPartnerMovements
            .IgnoreQueryFilters()
            .AnyAsync(movement => movement.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.BusinessPartners
            .IgnoreQueryFilters()
            .AnyAsync(partner => partner.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(balance => balance.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.ItemMovements
            .IgnoreQueryFilters()
            .AnyAsync(movement => movement.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(balance => balance.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockOpeningBalanceLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockAdjustments
            .IgnoreQueryFilters()
            .AnyAsync(adjustment => adjustment.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockAdjustmentLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockTransfers
            .IgnoreQueryFilters()
            .AnyAsync(transfer => transfer.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.StockTransferLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.InventoryCounts
            .IgnoreQueryFilters()
            .AnyAsync(count => count.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.InventoryCountLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.ItemStoreBalances
            .IgnoreQueryFilters()
            .AnyAsync(balance => balance.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.InventoryCostAllocations
            .IgnoreQueryFilters()
            .AnyAsync(allocation => allocation.CompanyId == targetCompanyId, cancellationToken) ||
        await dbContext.DriverTrips
            .IgnoreQueryFilters()
            .AnyAsync(trip => trip.CompanyId == targetCompanyId, cancellationToken);

    private async Task<string?> FindCompanyDependencyAsync(
        int targetCompanyId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Invoices.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "invoice";
        if (await dbContext.InvoiceLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "invoice line";
        if (await dbContext.InvoiceContainerLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "invoice container line";
        if (await dbContext.InvoicePayments.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "invoice payment";
        if (await dbContext.ExchangeRates.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "exchange-rate";
        if (await dbContext.PartnerOpeningBalances.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "partner opening-balance";
        if (await dbContext.BusinessPartnerMovements.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "partner movement";
        if (await dbContext.ItemMovements.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "item movement";
        if (await dbContext.StockOpeningBalances.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock opening-balance";
        if (await dbContext.StockOpeningBalanceLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock opening-balance line";
        if (await dbContext.StockAdjustments.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock adjustment";
        if (await dbContext.StockAdjustmentLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock adjustment line";
        if (await dbContext.StockTransfers.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock transfer";
        if (await dbContext.StockTransferLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "stock transfer line";
        if (await dbContext.InventoryCounts.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "inventory count";
        if (await dbContext.InventoryCountLines.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "inventory count line";
        if (await dbContext.ItemStoreBalances.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "item-store balance";
        if (await dbContext.InventoryCostAllocations.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "inventory cost-allocation";
        if (await dbContext.Cashboxes.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "cashbox";
        if (await dbContext.CashVouchers.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "cash voucher";
        if (await dbContext.UserCompanies.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "user-company assignment";
        if (await dbContext.RefreshTokens.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "refresh token";
        if (await dbContext.DriverTrips.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "driver trip";
        if (await dbContext.ContainerMovements.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "container movement";
        if (await dbContext.Items.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "item";
        if (await dbContext.ItemUnits.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "item unit";
        if (await dbContext.ItemsCategories.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "item category";
        if (await dbContext.BusinessPartners.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "business partner";
        if (await dbContext.Drivers.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "driver";
        if (await dbContext.Stores.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "store";
        if (await dbContext.Containers.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "container";
        if (await dbContext.StoreContainers.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "store-container assignment";
        if (await dbContext.CashMovementTypes.IgnoreQueryFilters().AnyAsync(
                entity => entity.CompanyId == targetCompanyId, cancellationToken)) return "cash movement type";

        return null;
    }

}
