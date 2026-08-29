using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLinePricingExpenseConfiguration
    : AuditableEntityConfiguration<InvoiceLinePricingExpense>
{
    public override void Configure(
        EntityTypeBuilder<InvoiceLinePricingExpense> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "InvoiceLinePricingExpenses",
            table => table.HasCheckConstraint(
                "CK_InvoiceLinePricingExpenses_Amount_Positive",
                "[Amount] > 0"));

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Id)
            .ValueGeneratedOnAdd();

        builder.Property(expense => expense.CompanyId)
            .IsRequired();

        builder.Property(expense => expense.InvoiceLineId)
            .IsRequired();

        builder.Property(expense => expense.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(expense => expense.Amount)
            .HasPrecision(
                InventoryCostRules.ValuePrecision,
                InventoryCostRules.ValueScale)
            .IsRequired();

        builder.Property(expense => expense.Notes)
            .HasMaxLength(1_000);

        builder.HasOne(expense => expense.Company)
            .WithMany()
            .HasForeignKey(expense => expense.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(expense => expense.InvoiceLine)
            .WithMany(line => line.PricingExpenses)
            .HasForeignKey(expense => new
            {
                expense.CompanyId,
                expense.InvoiceLineId
            })
            .HasPrincipalKey(line => new
            {
                line.CompanyId,
                line.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => new
        {
            expense.CompanyId,
            expense.InvoiceLineId,
            expense.Name
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
