namespace MiniErp.Domain.Entities.Inventory;

public static class StockOpeningBalanceCarryForwardRules
{
    public const string MarkerPrefix = "AUTO_FY_INVENTORY_CARRY:";
    public const string DocumentNumberPrefix = "FYOB-";

    // The generated document number survives edits to legacy notes.
    public static bool IsCarriedForward(string documentNumber, string? notes) =>
        documentNumber.StartsWith(DocumentNumberPrefix, StringComparison.OrdinalIgnoreCase) ||
        (notes?.StartsWith(MarkerPrefix, StringComparison.OrdinalIgnoreCase) ?? false);

    public static string BuildDocumentNumber(int sourceFiscalYearId, int storeId) =>
        $"{DocumentNumberPrefix}{sourceFiscalYearId}-{storeId}";
}
