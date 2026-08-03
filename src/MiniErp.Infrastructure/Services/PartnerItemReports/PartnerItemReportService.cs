using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PartnerItemReports;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.PartnerItemReports.PartnerItemReportErrors;

namespace MiniErp.Infrastructure.Services.PartnerItemReports;

public sealed class PartnerItemReportService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IPartnerItemReportService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PartnerItemReportResponse>> GetAsync(
        PartnerItemReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        if (filters.BusinessPartnerId is <= 0)
        {
            return Result<PartnerItemReportResponse>.Failure(
                BusinessPartnerRequired());
        }

        if (filters.ItemId.HasValue && filters.ItemId.Value <= 0)
        {
            return Result<PartnerItemReportResponse>.Failure(ItemInvalid());
        }

        if (filters.CountryId.HasValue && filters.CountryId.Value <= 0)
        {
            return Result<PartnerItemReportResponse>.Failure(CountryInvalid());
        }

        var search = filters.Search?.Trim();
        if (search?.Length > 256)
        {
            return Result<PartnerItemReportResponse>.Failure(SearchTooLong());
        }

        if (filters.MovementType.HasValue &&
            filters.MovementType is not InvoiceType.Sales and not InvoiceType.Purchase)
        {
            return Result<PartnerItemReportResponse>.Failure(InvalidMovementType());
        }

        if (filters.FromDate.HasValue &&
            filters.ToDate.HasValue &&
            filters.FromDate.Value > filters.ToDate.Value)
        {
            return Result<PartnerItemReportResponse>.Failure(
                InvalidDateRange());
        }

        string? partnerName = null;
        if (filters.BusinessPartnerId is int businessPartnerId)
        {
            var partner = await dbContext.BusinessPartners
                .AsNoTracking()
                .Where(entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == businessPartnerId &&
                    !entity.IsDeleted)
                .Select(entity => new { entity.Id, entity.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (partner is null)
            {
                return Result<PartnerItemReportResponse>.Failure(
                    BusinessPartnerNotFound());
            }

            partnerName = partner.Name;
        }

        var item = filters.ItemId.HasValue
            ? await dbContext.Items
                .AsNoTracking()
                .Where(entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == filters.ItemId.Value &&
                    !entity.IsDeleted)
                .Select(entity => new { entity.Id, entity.Name })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        if (filters.ItemId.HasValue && item is null)
        {
            return Result<PartnerItemReportResponse>.Failure(ItemNotFound());
        }

        var query = dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.Invoice.CompanyId == companyId &&
                (!filters.BusinessPartnerId.HasValue ||
                 line.Invoice.BusinessPartnerId == filters.BusinessPartnerId.Value) &&
                (!filters.CountryId.HasValue ||
                 line.Invoice.CountryId == filters.CountryId.Value) &&
                !line.IsDeleted &&
                !line.Invoice.IsDeleted &&
                (line.Invoice.InvoiceType == InvoiceType.Sales ||
                 line.Invoice.InvoiceType == InvoiceType.Purchase) &&
                (!filters.MovementType.HasValue ||
                 line.Invoice.InvoiceType == filters.MovementType.Value) &&
                (string.IsNullOrEmpty(search) ||
                 line.Invoice.InvoiceNumber.Contains(search) ||
                 (line.Invoice.PartnerInvoiceNo != null &&
                  line.Invoice.PartnerInvoiceNo.Contains(search)) ||
                 (line.Invoice.Notes != null &&
                  line.Invoice.Notes.Contains(search))));

        if (item is not null)
        {
            query = query.Where(line => line.ItemId == item.Id);
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(line =>
                line.Invoice.InvoiceDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(line =>
                line.Invoice.InvoiceDate <= filters.ToDate.Value);
        }

        var rows = await query
            .OrderBy(line => line.Invoice.InvoiceDate)
            .ThenBy(line => line.Invoice.Id)
            .ThenBy(line => line.Id)
            .Select(line => new
            {
                line.ItemId,
                ItemName = line.Item != null
                    ? line.Item.Name
                    : line.ItemName ?? string.Empty,
                InvoiceId = line.Invoice.Id,
                line.Invoice.InvoiceNumber,
                line.Invoice.InvoiceDate,
                line.Invoice.InvoiceType,
                line.Count,
                line.Weight,
                line.Quantity,
                line.Price,
                line.Total
            })
            .ToListAsync(cancellationToken);

        // InvoiceLine is the source of truth for the report amounts:
        // quantity = count * weight and total = quantity * unit price.
        var movements = rows
            .Select(row => new PartnerItemReportMovementResponse(
                ItemId: row.ItemId,
                ItemName: row.ItemName,
                InvoiceId: row.InvoiceId,
                InvoiceNumber: row.InvoiceNumber,
                InvoiceDate: row.InvoiceDate,
                MovementType: row.InvoiceType == InvoiceType.Sales
                    ? "sale"
                    : "purchase",
                Count: row.Count,
                Weight: row.Weight,
                Quantity: row.Quantity,
                UnitPrice: row.Price,
                TotalAmount: row.Total))
            .ToArray();

        var summary = new PartnerItemReportSummaryResponse(
            TotalSalesQuantity: movements
                .Where(movement => movement.MovementType == "sale")
                .Sum(movement => movement.Quantity),
            TotalPurchaseQuantity: movements
                .Where(movement => movement.MovementType == "purchase")
                .Sum(movement => movement.Quantity),
            TotalSalesWeight: movements
                .Where(movement => movement.MovementType == "sale")
                .Sum(movement => movement.Weight),
            TotalPurchaseWeight: movements
                .Where(movement => movement.MovementType == "purchase")
                .Sum(movement => movement.Weight));

        return Result<PartnerItemReportResponse>.Success(
            new PartnerItemReportResponse(
                BusinessPartnerId: filters.BusinessPartnerId,
                BusinessPartnerName: partnerName,
                ItemId: item?.Id,
                ItemName: item?.Name,
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                Summary: summary,
                Movements: movements));
    }
}
