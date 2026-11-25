using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Inventory.InventoryErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed partial class InventoryCostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IInventoryCostPostingSynchronizer?
        inventoryCostPostingSynchronizer = null,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : IInventoryCostingService
{
    private const string CostFieldName = "unitCost";
    private readonly int companyId = currentCompanyContext.CompanyId;
}
