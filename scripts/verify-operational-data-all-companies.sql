/*
Read-only verification for seed-operational-data-all-companies.ps1.
Change @Prefix to the same value passed to the PowerShell script.
*/
DECLARE @Prefix nvarchar(100) = N'AUTO-DEMO';
DECLARE @NamePattern nvarchar(202) = @Prefix + N' %';
DECLARE @NumberPattern nvarchar(202) = @Prefix + N'-C%';

SELECT
    c.Id AS CompanyId,
    c.Name AS CompanyName,
    (SELECT COUNT(*) FROM BusinessPartners bp
        WHERE bp.CompanyId = c.Id AND bp.IsDeleted = 0
            AND bp.Name LIKE @NamePattern) AS BusinessPartners,
    (SELECT COUNT(*) FROM Items i
        WHERE i.CompanyId = c.Id AND i.IsDeleted = 0
            AND i.Name LIKE @NamePattern) AS Items,
    (SELECT COUNT(*) FROM Invoices inv
        WHERE inv.CompanyId = c.Id AND inv.IsDeleted = 0
            AND inv.InvoiceNumber LIKE @NumberPattern) AS Invoices,
    (SELECT COUNT(*) FROM CashVouchers cv
        WHERE cv.CompanyId = c.Id AND cv.IsDeleted = 0
            AND cv.Description LIKE @NamePattern) AS CashVouchers,
    (SELECT COUNT(*) FROM JournalEntries je
        WHERE je.CompanyId = c.Id AND je.IsDeleted = 0
            AND (
                je.Description LIKE @NamePattern
                OR (je.SourceType = 1 AND EXISTS (
                    SELECT 1 FROM Invoices sourceInvoice
                    WHERE sourceInvoice.Id = je.SourceId
                        AND sourceInvoice.CompanyId = c.Id
                        AND sourceInvoice.IsDeleted = 0
                        AND sourceInvoice.InvoiceNumber LIKE @NumberPattern))
                OR (je.SourceType = 2 AND EXISTS (
                    SELECT 1 FROM CashVouchers sourceVoucher
                    WHERE sourceVoucher.Id = je.SourceId
                        AND sourceVoucher.CompanyId = c.Id
                        AND sourceVoucher.IsDeleted = 0
                        AND sourceVoucher.Description LIKE @NamePattern))
                OR (je.SourceType = 11 AND EXISTS (
                    SELECT 1 FROM Cashboxes sourceCashbox
                    WHERE sourceCashbox.Id = je.SourceId
                        AND sourceCashbox.CompanyId = c.Id
                        AND sourceCashbox.IsDeleted = 0
                        AND sourceCashbox.Name LIKE @NamePattern))
            )) AS RelatedJournalEntries,
    (SELECT COUNT(*) FROM Drivers d
        WHERE d.CompanyId = c.Id AND d.IsDeleted = 0
            AND d.Name LIKE @NamePattern) AS Drivers,
    (SELECT COUNT(*) FROM Employees e
        WHERE e.CompanyId = c.Id AND e.IsDeleted = 0
            AND e.Name LIKE @NamePattern) AS Employees
FROM Companies c
WHERE c.IsDeleted = 0
ORDER BY c.Id;
