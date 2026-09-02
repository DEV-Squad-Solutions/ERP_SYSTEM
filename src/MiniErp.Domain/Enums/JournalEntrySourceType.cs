namespace MiniErp.Domain.Enums;

public enum JournalEntrySourceType
{
    Invoice = 1,
    CashVoucher = 2,
    CashboxTransfer = 3,
    ItemMovement = 4,
    StockAdjustment = 5,
    StockOpeningBalance = 6,
    InventoryCount = 7,
    PayrollEntry = 8,
    PartnerOpeningBalance = 9,
    EmployeeOpeningBalance = 10,
    CashboxOpeningBalance = 11,
    DriverTrip = 12,
    FiscalYearClosing = 13
}
