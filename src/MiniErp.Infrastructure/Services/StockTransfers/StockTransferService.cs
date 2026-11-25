using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockTransfers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.StockTransfers.StockTransferErrors;

namespace MiniErp.Infrastructure.Services.StockTransfers;

public sealed partial class StockTransferService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService,
    TimeProvider timeProvider,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : IStockTransferService, IScopedService
{
    private static readonly ItemMovementType[] TransferMovementTypes =
    [
        ItemMovementType.TransferOut,
        ItemMovementType.TransferIn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;
}
