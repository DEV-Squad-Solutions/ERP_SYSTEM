using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxTransferServiceTests
{
    static CashboxTransferServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task CreateAtomicallyPostsPaymentAndReceipt()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxTransferService(companyId: 1);

        var result = await service.AddAsync(CreateRequest(amount: 200m));

        Assert.True(result.IsSuccess);
        Assert.Matches("^TRF-[0-9]{4,}$", result.Value.TransferNumber);
        Assert.Equal(200m, result.Value.Amount);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(1m, result.Value.ExchangeRate);
        Assert.Equal(200m, result.Value.BaseAmount);

        var vouchers = await database.Context.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CashboxTransferId == result.Value.Id)
            .OrderBy(voucher => voucher.Direction)
            .ToListAsync();
        Assert.Equal(2, vouchers.Count);
        Assert.All(vouchers, voucher =>
        {
            Assert.False(voucher.IsDraft);
            Assert.Null(voucher.CashMovementTypeId);
            Assert.Equal(CashPartyType.None, voucher.PartyType);
            Assert.Equal(result.Value.TransferNumber, voucher.ReferenceNumber);
        });
        var paymentVoucher = Assert.Single(vouchers, voucher =>
            voucher.Id == result.Value.PaymentVoucherId);
        Assert.Equal(1, paymentVoucher.CashboxId);
        Assert.Equal(CashDirection.Payment, paymentVoucher.Direction);
        Assert.Matches("^PAY-[0-9]{4,}$", paymentVoucher.VoucherNumber);

        var receiptVoucher = Assert.Single(vouchers, voucher =>
            voucher.Id == result.Value.ReceiptVoucherId);
        Assert.Equal(2, receiptVoucher.CashboxId);
        Assert.Equal(CashDirection.Receipt, receiptVoucher.Direction);
        Assert.Matches("^RCV-[0-9]{4,}$", receiptVoucher.VoucherNumber);

        var source = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(2);
        Assert.Equal(800m, source.Value.CurrentBalance);
        Assert.Equal(700m, destination.Value.CurrentBalance);

        var page = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 10
            },
            new CashboxTransferFilterRequest(Search: "Main"));
        var listItem = Assert.Single(page.Value.Items);
        Assert.Equal(result.Value.Id, listItem.Id);
        Assert.Equal(200m, listItem.Amount);
        Assert.Equal("Main Cashbox", listItem.SourceCashboxName);
        Assert.Equal("Second Cashbox", listItem.DestinationCashboxName);

        var otherCompany = await database
            .CreateCashboxTransferService(companyId: 2)
            .GetByIdAsync(result.Value.Id);
        Assert.Equal("CashboxTransfers.NotFound", otherCompany.Error.Code);
    }

    [Theory]
    [InlineData(1, 1, 10, "CashboxTransfers.CashboxesMustDiffer")]
    [InlineData(1, 5, 10, "CashboxTransfers.DestinationAmountRequired")]
    [InlineData(1, 2, 1001, "CashboxTransfers.InsufficientCashboxBalance")]
    [InlineData(4, 2, 10, "CashboxTransfers.CashboxNotFound")]
    public async Task CreateRejectsInvalidOrUnsafeTransfer(
        int sourceCashboxId,
        int destinationCashboxId,
        decimal amount,
        string expectedCode)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxTransferService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(
                sourceCashboxId,
                destinationCashboxId,
                amount));

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(await database.Context.CashboxTransfers.ToListAsync());
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task CreateSupportsDifferentCurrenciesWithDestinationAmount()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxTransferService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(
                sourceCashboxId: 1,
                destinationCashboxId: 5,
                amount: 250m) with
            {
                DestinationAmount = 5m
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(250m, result.Value.Amount);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(5m, result.Value.DestinationAmount);
        Assert.Equal(CurrencyCode.USD, result.Value.DestinationCurrency);
        Assert.Equal(50m, result.Value.DestinationExchangeRate);
        Assert.Equal(250m, result.Value.BaseAmount);
        Assert.Equal(250m, result.Value.DestinationBaseAmount);

        var vouchers = await database.Context.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CashboxTransferId == result.Value.Id)
            .ToListAsync();
        var payment = Assert.Single(vouchers, voucher =>
            voucher.Direction == CashDirection.Payment);
        var receipt = Assert.Single(vouchers, voucher =>
            voucher.Direction == CashDirection.Receipt);
        Assert.Equal(250m, payment.Amount);
        Assert.Equal(CurrencyCode.EGP, payment.Currency);
        Assert.Equal(5m, receipt.Amount);
        Assert.Equal(CurrencyCode.USD, receipt.Currency);
        Assert.Equal(50m, receipt.ExchangeRate);
        Assert.Equal(250m, receipt.BaseAmount);

        var source = await database.CreateCashboxService(1).GetByIdAsync(1);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(5);
        Assert.Equal(750m, source.Value.CurrentBalance);
        Assert.Equal(105m, destination.Value.CurrentBalance);
    }

    [Fact]
    public async Task CreateCalculatesDestinationAmountFromConversionRate()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxTransferService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(
                sourceCashboxId: 5,
                destinationCashboxId: 1,
                amount: 5m) with
            {
                ConversionRate = 50m
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, result.Value.Amount);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(50m, result.Value.ConversionRate);
        Assert.Equal(250m, result.Value.DestinationAmount);
        Assert.Equal(CurrencyCode.EGP, result.Value.DestinationCurrency);
        Assert.Equal(50m, result.Value.ExchangeRate);
        Assert.Equal(1m, result.Value.DestinationExchangeRate);
        Assert.Equal(250m, result.Value.BaseAmount);
        Assert.Equal(250m, result.Value.DestinationBaseAmount);

        var source = await database.CreateCashboxService(1).GetByIdAsync(5);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(95m, source.Value.CurrentBalance);
        Assert.Equal(1250m, destination.Value.CurrentBalance);
    }

    [Fact]
    public async Task UpdateSynchronizesBothVouchersAndBalances()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateCashboxTransferService(1)
            .AddAsync(CreateRequest(amount: 100m));

        await using var updateContext = database.CreateAdditionalContext();
        var updated = await database.CreateCashboxTransferService(
                companyId: 1,
                updateContext)
            .UpdateAsync(
                created.Value.Id,
                new CashboxTransferUpdateRequest(
                    TransferDate: new DateOnly(2026, 8, 9),
                    SourceCashboxId: 2,
                    DestinationCashboxId: 1,
                    Amount: 300m,
                    Description: "Reverse direction",
                    Notes: "Updated",
                    RowVersion: created.Value.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Value.TransferNumber, updated.Value.TransferNumber);
        Assert.Equal(created.Value.PaymentVoucherId, updated.Value.PaymentVoucherId);
        Assert.Equal(created.Value.ReceiptVoucherId, updated.Value.ReceiptVoucherId);
        Assert.Equal(2, updated.Value.SourceCashboxId);
        Assert.Equal(1, updated.Value.DestinationCashboxId);
        Assert.Equal(300m, updated.Value.Amount);

        var source = await database.CreateCashboxService(1)
            .GetByIdAsync(2);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(200m, source.Value.CurrentBalance);
        Assert.Equal(1300m, destination.Value.CurrentBalance);
    }

    [Fact]
    public async Task UpdateSupportsDifferentCurrenciesWithDestinationAmount()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateCashboxTransferService(1)
            .AddAsync(CreateRequest(amount: 100m));

        await using var updateContext = database.CreateAdditionalContext();
        var result = await database.CreateCashboxTransferService(
                companyId: 1,
                updateContext)
            .UpdateAsync(
                created.Value.Id,
                new CashboxTransferUpdateRequest(
                    TransferDate: new DateOnly(2026, 8, 9),
                    SourceCashboxId: 5,
                    DestinationCashboxId: 1,
                    Amount: 5m,
                    Description: "USD to EGP",
                    Notes: null,
                    RowVersion: created.Value.RowVersion,
                    ExchangeRate: 50m,
                    DestinationAmount: 250m));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(5m, result.Value.Amount);
        Assert.Equal(CurrencyCode.EGP, result.Value.DestinationCurrency);
        Assert.Equal(250m, result.Value.DestinationAmount);
        Assert.Equal(50m, result.Value.ExchangeRate);
        Assert.Equal(1m, result.Value.DestinationExchangeRate);
        Assert.Equal(250m, result.Value.BaseAmount);
        Assert.Equal(250m, result.Value.DestinationBaseAmount);

        var source = await database.CreateCashboxService(1).GetByIdAsync(5);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(95m, source.Value.CurrentBalance);
        Assert.Equal(1250m, destination.Value.CurrentBalance);
    }

    [Fact]
    public async Task DeleteRemovesBothEffectsAndGeneratedVouchersAreReadOnly()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateCashboxTransferService(1)
            .AddAsync(CreateRequest(amount: 100m));

        await using (var voucherContext = database.CreateAdditionalContext())
        {
            var voucherService = database.CreateVoucherService(
                companyId: 1,
                voucherContext);
            var deleteVoucher = await voucherService.DeleteAsync(
                created.Value.PaymentVoucherId);

            Assert.Equal(
                "CashVouchers.TransferGeneratedReadOnly",
                deleteVoucher.Error.Code);
        }

        await using var deleteContext = database.CreateAdditionalContext();
        var deleted = await database.CreateCashboxTransferService(
                companyId: 1,
                deleteContext)
            .DeleteAsync(created.Value.Id);

        Assert.True(deleted.IsSuccess);
        var source = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        var destination = await database.CreateCashboxService(1)
            .GetByIdAsync(2);
        Assert.Equal(1000m, source.Value.CurrentBalance);
        Assert.Equal(500m, destination.Value.CurrentBalance);
        Assert.Equal(
            2,
            await database.Context.CashVouchers
                .IgnoreQueryFilters()
                .CountAsync(voucher =>
                    voucher.CashboxTransferId == created.Value.Id &&
                    voucher.IsDeleted));
    }

    [Fact]
    public async Task CashboxStatementShowsGeneratedTransferMovement()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.CreateCashboxTransferService(1)
            .AddAsync(CreateRequest(amount: 75m));

        var statement = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                new PaginationRequest
                {
                    PageNumber = 1,
                    PageSize = 10
                },
                new CashboxStatementFilterRequest(CashboxId: 1));

        Assert.True(statement.IsSuccess);
        var item = Assert.Single(statement.Value.Items);
        Assert.Equal("تحويل خزائن صادر", item.MovementName);
        Assert.Equal(75m, item.PaymentAmount);
        Assert.Equal(925m, item.Balance);
    }

    [Fact]
    public async Task DeleteRejectsRemovingReceiptNeededByLaterPayment()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateCashboxTransferService(1)
            .AddAsync(CreateRequest(amount: 100m));
        var laterPayment = new CashVoucher
        {
            CompanyId = 1,
            VoucherNumber = "LATER-PAYMENT",
            VoucherDate = new DateOnly(2026, 8, 10),
            Direction = CashDirection.Payment,
            CashboxId = 2,
            CashMovementTypeId = 4,
            PartyType = CashPartyType.None,
            Amount = 550m,
            Currency = CurrencyCode.EGP,
            Description = "Payment after transfer"
        };
        laterPayment.ApplyExchangeRate(
            exchangeRateId: null,
            exchangeRate: 1m);
        laterPayment.Touch(DateTime.UtcNow);
        database.Context.CashVouchers.Add(laterPayment);
        await database.Context.SaveChangesAsync();

        await using var deleteContext = database.CreateAdditionalContext();
        var result = await database.CreateCashboxTransferService(
                companyId: 1,
                deleteContext)
            .DeleteAsync(created.Value.Id);

        Assert.Equal(
            "CashboxTransfers.InsufficientCashboxBalance",
            result.Error.Code);
        Assert.True((await database.CreateCashboxTransferService(1)
            .GetByIdAsync(created.Value.Id)).IsSuccess);
    }

    private static CashboxTransferRequest CreateRequest(
        int sourceCashboxId = 1,
        int destinationCashboxId = 2,
        decimal amount = 100m) =>
        new(
            TransferDate: new DateOnly(2026, 8, 8),
            SourceCashboxId: sourceCashboxId,
            DestinationCashboxId: destinationCashboxId,
            Amount: amount,
            Description: "Internal transfer",
            Notes: null);
}
