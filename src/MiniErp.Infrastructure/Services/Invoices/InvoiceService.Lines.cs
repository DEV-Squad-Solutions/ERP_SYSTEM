using Mapster;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;

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
            line.CompanyId = companyId;
            line.ItemUnitId = preparation.ItemUnitIds[requestLine.ItemId];
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
        var incomingByItem = request.Lines.ToDictionary(line => line.ItemId);
        foreach (var existingLine in invoice.Lines.ToList())
        {
            if (!incomingByItem.TryGetValue(existingLine.ItemId, out var incoming))
            {
                dbContext.InvoiceLines.Remove(existingLine);
                invoice.Lines.Remove(existingLine);
                continue;
            }

            existingLine.Count = incoming.Count;
            existingLine.Weight = incoming.Weight;
            existingLine.Price = incoming.Price;
            existingLine.Notes = string.IsNullOrWhiteSpace(incoming.Notes)
                ? null
                : incoming.Notes.Trim();
            existingLine.ItemUnitId =
                preparation.ItemUnitIds[incoming.ItemId];
        }

        var existingItemIds = invoice.Lines
            .Where(line => !line.IsDeleted)
            .Select(line => line.ItemId)
            .ToHashSet();
        foreach (var incoming in request.Lines.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            var line = incoming.Adapt<InvoiceLine>();
            line.CompanyId = companyId;
            line.ItemUnitId = preparation.ItemUnitIds[incoming.ItemId];
            invoice.Lines.Add(line);
        }
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
