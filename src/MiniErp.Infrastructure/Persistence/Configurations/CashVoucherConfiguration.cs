using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CashVoucherConfiguration
    : AuditableEntityConfiguration<CashVoucher>
{
    public override void Configure(EntityTypeBuilder<CashVoucher> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "CashVouchers",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CashVouchers_Amount_Positive",
                    "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_CashVouchers_Direction",
                    "[Direction] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_CashVouchers_PartyType",
                    "[PartyType] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint(
                    "CK_CashVouchers_PartyShape",
                    "([PartyType] = 1 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND " +
                    "[DriverId] IS NULL AND [DriverTripId] IS NULL AND " +
                    "[ExternalPartyName] IS NULL) OR " +
                    "([PartyType] = 2 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NOT NULL AND " +
                    "[DriverId] IS NULL AND [DriverTripId] IS NULL AND " +
                    "[ExternalPartyName] IS NULL) OR " +
                    "([PartyType] = 3 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND " +
                    "[DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR " +
                    "([PartyType] = 4 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND " +
                    "[DriverId] IS NULL AND [DriverTripId] IS NULL AND " +
                    "[ExternalPartyName] IS NOT NULL) OR " +
                    "([PartyType] = 5 AND [EmployeeId] IS NOT NULL AND " +
                    "[BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND " +
                    "[DriverTripId] IS NULL AND [ExternalPartyName] IS NULL)");
                table.HasCheckConstraint(
                    "CK_CashVouchers_PostingReferencesTogether",
                    "[CashMovementTypeId] IS NULL OR [CashboxId] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_CashVouchers_TransferShape",
                    "[CashboxTransferId] IS NULL OR " +
                    "([CashboxId] IS NOT NULL AND " +
                    "[CashMovementTypeId] IS NULL AND " +
                    "[InvoiceId] IS NULL AND [PartyType] = 1)");
            });

        builder.HasKey(voucher => voucher.Id);

        builder.Property(voucher => voucher.Id)
            .ValueGeneratedOnAdd();

        builder.Property(voucher => voucher.CompanyId)
            .IsRequired();

        builder.Property(voucher => voucher.InvoiceId);

        builder.Property(voucher => voucher.CashboxTransferId);

        builder.HasAlternateKey(voucher => new
        {
            voucher.CompanyId,
            voucher.Id
        });

        builder.Property(voucher => voucher.VoucherNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.VoucherNumber
        })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.InvoiceId
        })
            .HasFilter("[InvoiceId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.CashboxTransferId,
            voucher.Direction
        })
            .IsUnique()
            .HasFilter(
                "[CashboxTransferId] IS NOT NULL AND [IsDeleted] = 0");

        builder.Property(voucher => voucher.VoucherDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(voucher => voucher.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(voucher => voucher.PartyType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(voucher => voucher.ExternalPartyName)
            .HasMaxLength(200);

        builder.Property(voucher => voucher.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(voucher => voucher.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(voucher => voucher.ExchangeRateId);

        builder.Property(voucher => voucher.ExchangeRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.RatePrecision,
                Domain.Entities.Companies.ExchangeRateRules.RateScale)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(voucher => voucher.BaseAmount)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(voucher => voucher.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(voucher => voucher.Description)
            .HasMaxLength(1_000);

        builder.Property(voucher => voucher.Notes)
            .HasMaxLength(1_000);

        builder.Property(voucher => voucher.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(voucher => voucher.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.CashboxId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.CashMovementTypeId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.BusinessPartnerId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.DriverId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.DriverTripId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.EmployeeId,
            voucher.VoucherDate,
            voucher.Id
        });

        builder.HasOne(voucher => voucher.Company)
            .WithMany()
            .HasForeignKey(voucher => voucher.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.ExchangeRateRecord)
            .WithMany()
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.ExchangeRateId
            })
            .HasPrincipalKey(rate => new
            {
                rate.CompanyId,
                rate.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(voucher => new
        {
            voucher.CompanyId,
            voucher.ExchangeRateId
        });

        builder.HasOne(voucher => voucher.Invoice)
            .WithMany(invoice => invoice.PaymentVouchers)
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.Cashbox)
            .WithMany(cashbox => cashbox.Vouchers)
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.CashboxId
            })
            .HasPrincipalKey(cashbox => new
            {
                cashbox.CompanyId,
                cashbox.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.CashMovementType)
            .WithMany(movementType => movementType.Vouchers)
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.CashMovementTypeId
            })
            .HasPrincipalKey(movementType => new
            {
                movementType.CompanyId,
                movementType.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.BusinessPartner)
            .WithMany()
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.Employee)
            .WithMany()
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.Driver)
            .WithMany()
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.DriverId
            })
            .HasPrincipalKey(driver => new
            {
                driver.CompanyId,
                driver.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(voucher => voucher.DriverTrip)
            .WithMany()
            .HasForeignKey(voucher => new
            {
                voucher.CompanyId,
                voucher.DriverTripId
            })
            .HasPrincipalKey(trip => new
            {
                trip.CompanyId,
                trip.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
