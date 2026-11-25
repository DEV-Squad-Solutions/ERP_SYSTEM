using System.Data;
using static MiniErp.Application.Features.StockAdjustments.StockAdjustmentErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockAdjustments;

public sealed partial class StockAdjustmentService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService,
    TimeProvider timeProvider,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard fiscalYearPeriodGuard,
    IInventoryPostingService? inventoryPostingService = null)
    : IStockAdjustmentService, IScopedService
{
    private static readonly ItemMovementType[] AdjustmentMovementTypes =
    [
        ItemMovementType.AdjustmentIncrease,
        ItemMovementType.AdjustmentDecrease
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;
}
