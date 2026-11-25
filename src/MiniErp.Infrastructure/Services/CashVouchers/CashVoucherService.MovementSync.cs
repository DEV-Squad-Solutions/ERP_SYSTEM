using System.Data;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed partial class CashVoucherService
{
    private BusinessPartnerMovement CreatePartnerMovement(
        CashVoucher voucher)
    {
        var debit = voucher.Direction == CashDirection.Payment
            ? voucher.PartnerAmount ?? voucher.Amount
            : 0m;
        var credit = voucher.Direction == CashDirection.Receipt
            ? voucher.PartnerAmount ?? voucher.Amount
            : 0m;

        var movement = new BusinessPartnerMovement
        {
            CompanyId = companyId,
            BusinessPartnerId = voucher.BusinessPartnerId!.Value,
            CashVoucherId = voucher.Id,
            CashVoucher = voucher,
            MovementType = voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment,
            MovementDate = voucher.VoucherDate,
            Currency = voucher.PartnerCurrency ?? voucher.Currency,
            Debit = debit,
            Credit = credit,
            Description = voucher.Description ??
                $"Cash voucher {voucher.VoucherNumber}"
        };
        movement.ApplyExchangeRate(voucher.PartnerExchangeRate ?? voucher.ExchangeRate);
        return movement;
    }

    private async Task SynchronizePartnerMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(
                movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist)
        {
            if (existing is not null)
            {
                dbContext.BusinessPartnerMovements.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            dbContext.BusinessPartnerMovements.Add(
                CreatePartnerMovement(voucher));
            return;
        }

        existing.BusinessPartnerId = voucher.BusinessPartnerId!.Value;
        existing.MovementType =
            voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.PartnerCurrency ?? voucher.Currency;
        existing.Debit = voucher.Direction == CashDirection.Payment
            ? voucher.PartnerAmount ?? voucher.Amount
            : 0m;
        existing.Credit = voucher.Direction == CashDirection.Receipt
            ? voucher.PartnerAmount ?? voucher.Amount
            : 0m;
        existing.ApplyExchangeRate(voucher.PartnerExchangeRate ?? voucher.ExchangeRate);
        existing.Description = voucher.Description ??
            $"Cash voucher {voucher.VoucherNumber}";
    }

    private async Task SynchronizeEmployeeMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        EmployeeMovementType? movementType,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmployeeMovements
            .FirstOrDefaultAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist || voucher.EmployeeId is null)
        {
            if (existing is not null)
            {
                dbContext.EmployeeMovements.Remove(existing);
            }

            return;
        }

        // Keep a safe fallback for internal callers and existing vouchers so
        // synchronization remains idempotent when they do not send a type.
        var effectiveType = movementType ??
            existing?.Type ??
            GetDefaultEmployeeMovementType(voucher.Direction);

        if (existing is null)
        {
            existing = new EmployeeMovement
            {
                CompanyId = companyId,
                CashVoucherId = voucher.Id
            };
            dbContext.EmployeeMovements.Add(existing);
        }

        existing.EmployeeId = voucher.EmployeeId.Value;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.Currency;
        existing.Notes = string.IsNullOrWhiteSpace(voucher.Notes)
            ? null
            : voucher.Notes.Trim();
        existing.ApplyAmounts(effectiveType, voucher.Amount);
        existing.ApplyExchangeRate(voucher.ExchangeRate);
    }
}
