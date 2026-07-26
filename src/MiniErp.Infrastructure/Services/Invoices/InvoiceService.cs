using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
    : IInvoiceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<InvoiceListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.CompanyId == companyId)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenByDescending(invoice => invoice.Id);

        return await paginationService.PaginateAsync<
            Invoice,
            InvoiceListResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<InvoiceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InvoiceResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<InvoiceResponse>.Failure(NotFound(id))
            : Result<InvoiceResponse>.Success(response);
    }

    public async Task<Result<InvoiceResponse>> AddAsync(
        InvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var invoice = request.Adapt<Invoice>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var preparation = await PrepareAsync(
            invoice,
            request.Lines,
            request.ContainerLines,
            currentInvoiceId: null,
            currentInvoiceNumber: null,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(preparation.Error);
        }

        invoice.CompanyId = companyId;
        invoice.InvoiceNumber = GenerateInvoiceNumber();
        invoice.Currency = preparation.Value.Currency;

        AddLines(invoice, request, preparation.Value);
        AddContainerLines(invoice, request);
        invoice.CalculateTotal();
        var amountError = ValidateAmounts(invoice);
        if (amountError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(amountError);
        }

        invoice.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SaveSideEffectsAsync(invoice, cancellationToken);

        var response = await ProjectResponseQuery(invoice.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<InvoiceResponse>.Success(response);
    }

    public async Task<Result<InvoiceResponse>> UpdateAsync(
        int id,
        InvoiceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InvoiceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<InvoiceResponse>.Failure(
                Error.Validation(
                    "Invoices.RowVersionRequired",
                    "يجب إرسال إصدار السجل الحالي المكون من 8 بايت.",
                    nameof(InvoiceUpdateRequest.RowVersion)));
        }

        var requestedValues = request.Adapt<Invoice>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var invoice = await LoadForWriteAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result<InvoiceResponse>.Failure(NotFound(id));
        }

        if (!invoice.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<InvoiceResponse>.Failure(Concurrency());
        }

        var entry = dbContext.Entry(invoice);
        entry.Property(item => item.RowVersion).OriginalValue = request.RowVersion;

        var preparation = await PrepareAsync(
            requestedValues,
            request.Lines,
            request.ContainerLines,
            currentInvoiceId: id,
            currentInvoiceNumber: invoice.InvoiceNumber,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(preparation.Error);
        }

        request.Adapt(invoice);
        invoice.Currency = preparation.Value.Currency;

        ReplaceLines(invoice, request, preparation.Value);
        ReplaceContainerLines(invoice, request);
        invoice.CalculateTotal();
        var amountError = ValidateAmounts(invoice);
        if (amountError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(amountError);
        }

        invoice.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            await RemoveSideEffectsAsync(invoice, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await SaveSideEffectsAsync(invoice, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<InvoiceResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var invoice = await LoadForWriteAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (InvoiceMovementRules.IsInbound(invoice.InvoiceType))
        {
            var stockError = await ValidateStockAsync(
                invoice,
                [],
                currentInvoiceId: invoice.Id,
                currentInvoiceNumber: invoice.InvoiceNumber,
                cancellationToken);
            if (stockError is not null)
            {
                return Result.Failure(stockError);
            }
        }

        await RemoveSideEffectsAsync(invoice, cancellationToken);
        dbContext.InvoiceLines.RemoveRange(invoice.Lines);
        dbContext.InvoiceContainerLines.RemoveRange(invoice.ContainerLines);
        dbContext.Invoices.Remove(invoice);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
