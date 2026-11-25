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
    private static CashVoucherResponse CreateOpeningBalanceResponse(
        OpeningBalanceRow row,
        int companyId,
        CurrencyCode baseCurrency,
        int fiscalYearId,
        string fiscalYearName)
    {
        var amount = Math.Abs(row.Amount);
        var exchangeRate = row.Currency == baseCurrency
            ? 1m
            : row.ExchangeRate > 0m
                ? row.ExchangeRate
                : 1m;
        var baseAmount = row.Currency == baseCurrency
            ? amount
            : Math.Abs(row.BaseOpeningBalance) > 0m
                ? Math.Abs(row.BaseOpeningBalance)
                : ExchangeRateRules.ConvertToBase(amount, exchangeRate);

        return new CashVoucherResponse(
            Id: -row.CashboxId,
            CompanyId: companyId,
            VoucherNumber: $"OPENING-BALANCE-{row.CashboxCode}",
            VoucherDate: row.VoucherDate,
            Direction: row.Direction,
            CashboxId: row.CashboxId,
            CashboxName: row.CashboxName,
            CashMovementTypeId: null,
            CashMovementTypeName: "رصيد افتتاحي",
            Classification: null,
            PartyType: CashPartyType.None,
            EmployeeId: null,
            EmployeeName: null,
            BusinessPartnerId: null,
            BusinessPartnerName: null,
            DriverId: null,
            DriverName: null,
            DriverTripId: null,
            DriverTripInvoiceNumber: null,
            ExternalPartyName: null,
            Amount: amount,
            Currency: row.Currency,
            BaseCurrency: baseCurrency,
            ExchangeRate: exchangeRate,
            BaseAmount: baseAmount,
            ReferenceNumber: null,
            Description: "الرصيد الافتتاحي",
            Notes: null,
            RowVersion: [])
        {
            FiscalYearId = fiscalYearId,
            FiscalYearName = fiscalYearName,
            IsDraft = false,
            IsOpeningBalance = true,
            CashboxBalance = row.CashboxBalance
        };
    }

    private sealed record OpeningBalanceRow(
        int CashboxId,
        string CashboxName,
        string CashboxCode,
        DateOnly VoucherDate,
        CashDirection Direction,
        decimal Amount,
        CurrencyCode Currency,
        decimal ExchangeRate,
        decimal BaseOpeningBalance,
        decimal CashboxBalance);

    private IQueryable<CashVoucherResponse> ProjectResponseQuery(
        int id,
        int? fiscalYearId = null) =>
        dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.Id == id &&
                (!fiscalYearId.HasValue ||
                 voucher.FiscalYearId == fiscalYearId.Value))
            .ProjectToType<CashVoucherResponse>();

    private async Task ApplyPreparationAsync(
        CashVoucher voucher,
        VoucherPreparation preparation,
        CancellationToken cancellationToken)
    {
        if (preparation.Cashbox is not null &&
            preparation.ExchangeRate is not null)
        {
            voucher.Currency = preparation.Cashbox.Currency;
            voucher.ApplyExchangeRate(
                preparation.ExchangeRate.ExchangeRateId,
                preparation.ExchangeRate.Rate);
            if (preparation.BusinessPartner is not null &&
                preparation.PartnerExchangeRate is not null)
                voucher.ApplyPartnerSettlement(
                    preparation.BusinessPartner.Currency,
                    preparation.PartnerExchangeRate.ExchangeRateId,
                    preparation.PartnerExchangeRate.Rate);
            else
                voucher.ClearPartnerSettlement();
            return;
        }

        voucher.CashboxId = null;
        voucher.ClearPartnerSettlement();
        voucher.CashMovementTypeId = null;
        voucher.AccountId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.EmployeeId = null;
        voucher.BusinessPartnerId = null;
        voucher.DriverId = null;
        voucher.DriverTripId = null;
        voucher.ExternalPartyName = null;

        voucher.Currency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
    }

    private sealed record VoucherPreparation(
        Cashbox? Cashbox,
        CashPartyType PartyType,
        BusinessPartner? BusinessPartner,
        Driver? Driver,
        ResolvedExchangeRate? ExchangeRate,
        ResolvedExchangeRate? PartnerExchangeRate,
        CashMovementClassification? Classification);

    private static CashPartyType DerivePartyType(
        CashVoucherUpdateRequest request) =>
        request.EmployeeId.HasValue
            ? CashPartyType.Employee
            : request.BusinessPartnerId.HasValue
                ? CashPartyType.Partner
                : request.DriverId.HasValue
                    ? CashPartyType.Driver
                    : !string.IsNullOrWhiteSpace(request.ExternalPartyName)
                        ? CashPartyType.Other
                        : CashPartyType.None;

    private static bool HasAtMostOneTarget(CashVoucherUpdateRequest request)
    {
        var selectedPartyCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyCount <= 1;
    }

    private static bool HasExactlyOnePostingTarget(
        CashVoucherUpdateRequest request)
    {
        var selectedPartyOrAccountCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyOrAccountCount == 1 ||
            (selectedPartyOrAccountCount == 0 &&
             request.CashMovementTypeId.HasValue);
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
