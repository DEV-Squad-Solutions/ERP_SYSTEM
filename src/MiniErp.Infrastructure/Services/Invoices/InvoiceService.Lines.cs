using Mapster;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private void AddLines(
        Invoice invoice,
        InvoiceRequest request,
        PreparedInvoice preparation)
    {
        foreach (var requestLine in request.Lines)
        {
            var line = requestLine.Adapt<InvoiceLine>();
            TryGetEffectiveLineValues(
                requestLine,
                out var count,
                out var weight);
            line.Count = count;
            line.Weight = weight;
            line.CompanyId = companyId;

            if (requestLine.ItemId.HasValue)
            {
                line.ItemUnitId = preparation.ItemUnitIds[requestLine.ItemId.Value];
            }
            else
            {
                line.ItemName = requestLine.ItemName?.Trim();
            }

            ApplyReturnCostInput(
                invoice.InvoiceType,
                line,
                requestLine);
            invoice.Lines.Add(line);
        }
    }

    private void AddContainerLines(
        Invoice invoice,
        InvoiceRequest request)
    {
        foreach (var requestLine in request.ContainerLines)
        {
            var line = requestLine.Adapt<InvoiceContainerLine>();
            line.CompanyId = companyId;
            invoice.ContainerLines.Add(line);
        }
    }

    private void ReplaceLines(
        Invoice invoice,
        InvoiceUpdateRequest request,
        PreparedInvoice preparation)
    {
        var catalogLines = request.Lines.Where(line => line.ItemId.HasValue).ToList();
        var freeTextLines = request.Lines.Where(line => !line.ItemId.HasValue).ToList();

        var incomingByItem = catalogLines.ToDictionary(line => line.ItemId!.Value);

        // Update or remove existing catalog-based lines
        foreach (var existingLine in invoice.Lines.Where(l => l.ItemId.HasValue).ToList())
        {
            if (!incomingByItem.TryGetValue(existingLine.ItemId!.Value, out var incoming))
            {
                dbContext.InvoiceLines.Remove(existingLine);
                invoice.Lines.Remove(existingLine);
                continue;
            }

            TryGetEffectiveLineValues(
                incoming,
                out var count,
                out var weight);
            existingLine.Count = count;
            existingLine.Weight = weight;
            existingLine.Price = incoming.Price;
            ApplyReturnCostInput(
                invoice.InvoiceType,
                existingLine,
                incoming);
            existingLine.Notes = string.IsNullOrWhiteSpace(incoming.Notes)
                ? null
                : incoming.Notes.Trim();
            existingLine.ItemUnitId =
                preparation.ItemUnitIds[incoming.ItemId!.Value];
        }

        // Remove all existing free-text lines (they'll be re-added)
        foreach (var existingFreeText in invoice.Lines.Where(l => !l.ItemId.HasValue).ToList())
        {
            dbContext.InvoiceLines.Remove(existingFreeText);
            invoice.Lines.Remove(existingFreeText);
        }

        // Add new catalog lines
        var existingItemIds = invoice.Lines
            .Where(line => !line.IsDeleted && line.ItemId.HasValue)
            .Select(line => line.ItemId!.Value)
            .ToHashSet();
        foreach (var incoming in catalogLines.Where(line =>
                     !existingItemIds.Contains(line.ItemId!.Value)))
        {
            var line = incoming.Adapt<InvoiceLine>();
            TryGetEffectiveLineValues(
                incoming,
                out var count,
                out var weight);
            line.Count = count;
            line.Weight = weight;
            line.CompanyId = companyId;
            line.ItemUnitId = preparation.ItemUnitIds[incoming.ItemId!.Value];
            ApplyReturnCostInput(
                invoice.InvoiceType,
                line,
                incoming);
            invoice.Lines.Add(line);
        }

        // Add all free-text lines
        foreach (var incoming in freeTextLines)
        {
            var line = incoming.Adapt<InvoiceLine>();
            TryGetEffectiveLineValues(
                incoming,
                out var count,
                out var weight);
            line.Count = count;
            line.Weight = weight;
            line.CompanyId = companyId;
            line.ItemName = incoming.ItemName?.Trim();
            ApplyReturnCostInput(
                invoice.InvoiceType,
                line,
                incoming);
            invoice.Lines.Add(line);
        }
    }

    private static void ApplyReturnCostInput(
        InvoiceType invoiceType,
        InvoiceLine line,
        InvoiceLineRequest request)
    {
        if (invoiceType == InvoiceType.SalesReturn)
        {
            line.SourceInvoiceLineId = request.SourceInvoiceLineId;
            line.ReturnUnitCost = request.ReturnUnitCost;
            return;
        }

        line.SourceInvoiceLineId = null;
        line.ReturnUnitCost = null;
    }

    private void ReplaceContainerLines(
        Invoice invoice,
        InvoiceUpdateRequest request)
    {
        var incomingByContainer = request.ContainerLines.ToDictionary(
            line => line.ContainerId);
        foreach (var existingLine in invoice.ContainerLines.ToList())
        {
            if (!incomingByContainer.TryGetValue(
                    existingLine.ContainerId,
                    out var incoming))
            {
                dbContext.InvoiceContainerLines.Remove(existingLine);
                continue;
            }

            existingLine.OutgoingUnits = incoming.OutgoingUnits;
            existingLine.IncomingUnits = incoming.IncomingUnits;
        }

        var existingContainerIds = invoice.ContainerLines
            .Where(line => !line.IsDeleted)
            .Select(line => line.ContainerId)
            .ToHashSet();
        foreach (var incoming in request.ContainerLines.Where(line =>
                     !existingContainerIds.Contains(line.ContainerId)))
        {
            var line = incoming.Adapt<InvoiceContainerLine>();
            line.CompanyId = companyId;
            invoice.ContainerLines.Add(line);
        }
    }
}
