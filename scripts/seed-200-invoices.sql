/*
    MiniErp - generate 200 invoice test records

    What this script does:
      - Inserts 200 invoice headers and 200 detail records (item or container).
      - Uses the active companies, partners, stores, items and item units already
        available in the database.
      - Creates 50 records of each invoice type:
          1 = Sales
          2 = Purchase
          3 = SalesReturn
          4 = PurchaseReturn
      - Creates fully paid cash item invoices plus unpaid and partially paid
        credit item invoices through PaidAmount.
      - Creates ItemMovements for every item invoice line.
      - Creates a BusinessPartnerMovement for every positive invoice balance
        and another movement for every linked payment voucher.
      - Creates CashVouchers and InvoicePayments for every non-zero paid row;
        the script rolls back if an active EGP cashbox, the invoice type's
        active default movement type or (for payments) enough cashbox balance
        is unavailable.
      - Adds customer container stores and InvoiceContainerLines to eligible
        sales and sales-return invoices, with matching ContainerMovements.
      - Links every item return line to an earlier source invoice line.
      - Uses existing customer container stores and active container assignments
        for sales and sales-return container invoices.
      - Populates all invoice, line, movement, voucher and payment columns that
        can be safely filled without inventing drivers or exchange-rate records.

    Safety:
      - No existing business rows are deleted or updated.
      - The script is transactional and refuses to run if this seed prefix
        already exists, so a second run cannot create duplicate test data.

    Important:
      PaymentStatus is computed by the application from PaidAmount; it is not a
      database column.

      ItemMovement cost snapshot fields are populated from the synthetic line
      price so the movement rows are immediately valid. InvoiceService can
      still run its normal costing replay later if you want FIFO/average-cost
      recalculation across the existing inventory timeline. DriverTrips are
      omitted because the seed does not invent driver assignments.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @InvoiceCount int = 200;
DECLARE @SeedPrefix nvarchar(100) = N'SQL-SEED-200-';
DECLARE @Today date = CONVERT(date, GETDATE());
DECLARE @Now datetime2(7) = SYSUTCDATETIME();
DECLARE @CreatedById nvarchar(450) = N'sql-seed-200-invoices';
DECLARE @CreatedByPc nvarchar(255) = LEFT(COALESCE(HOST_NAME(), N'SQL-SEED'), 255);
DECLARE @PoolCount int;
DECLARE @ApplicationLockResult int;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC @ApplicationLockResult = sys.sp_getapplock
        @Resource = N'MiniErp:SQL-SEED-200-INVOICES',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 10000;

    IF @ApplicationLockResult < 0
    BEGIN
        RAISERROR (N'Could not acquire the SQL-SEED-200 transaction lock. No rows were changed.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Invoices WITH (UPDLOCK, HOLDLOCK)
        WHERE ExportInvoiceCode LIKE @SeedPrefix + N'%'
           OR InvoiceNumber LIKE @SeedPrefix + N'%'
    )
    BEGIN
        RAISERROR (N'The SQL-SEED-200 invoice data already exists. No rows were changed.', 16, 1);
    END;

    CREATE TABLE #SeedPool
    (
        PoolIndex int NOT NULL PRIMARY KEY,
        CompanyId int NOT NULL,
        BusinessPartnerId int NOT NULL,
        StoreId int NOT NULL,
        CountryId int NULL,
        ItemsCategoryId int NULL,
        ItemId int NOT NULL,
        ItemUnitId int NOT NULL,
        ItemName nvarchar(200) NOT NULL,
        ContainerStoreId int NULL,
        ContainerId int NULL
    );

    INSERT INTO #SeedPool
    (
        PoolIndex,
        CompanyId,
        BusinessPartnerId,
        StoreId,
        CountryId,
        ItemsCategoryId,
        ItemId,
        ItemUnitId,
        ItemName,
        ContainerStoreId,
        ContainerId
    )
    SELECT
        CONVERT(int, ROW_NUMBER() OVER (ORDER BY company.Id, item.Id)),
        company.Id,
        partner.BusinessPartnerId,
        store.StoreId,
        country.CountryId,
        category.ItemsCategoryId,
        item.Id,
        itemUnit.Id,
        item.Name,
        containerAssignment.ContainerStoreId,
        containerAssignment.ContainerId
    FROM dbo.Companies AS company
    CROSS APPLY
    (
        SELECT TOP (1)
            businessPartner.Id AS BusinessPartnerId
        FROM dbo.BusinessPartners AS businessPartner
        WHERE businessPartner.CompanyId = company.Id
          AND businessPartner.IsActive = 1
          AND businessPartner.IsDeleted = 0
          AND businessPartner.Currency = 1 -- CurrencyCode.EGP
        ORDER BY businessPartner.Id
    ) AS partner
    CROSS APPLY
    (
        SELECT TOP (1)
            stockStore.Id AS StoreId
        FROM dbo.Stores AS stockStore
        WHERE stockStore.CompanyId = company.Id
          AND stockStore.IsContainerStore = 0
          AND stockStore.IsActive = 1
          AND stockStore.IsDeleted = 0
        ORDER BY stockStore.Id
    ) AS store
    INNER JOIN dbo.Items AS item
        ON item.CompanyId = company.Id
       AND item.IsActive = 1
       AND item.IsDeleted = 0
    INNER JOIN dbo.ItemUnits AS itemUnit
        ON itemUnit.CompanyId = item.CompanyId
       AND itemUnit.Id = item.ItemUnitId
       AND itemUnit.IsActive = 1
       AND itemUnit.IsDeleted = 0
    OUTER APPLY
    (
        SELECT TOP (1)
            countryReference.Id AS CountryId
        FROM dbo.Countries AS countryReference
        WHERE countryReference.IsActive = 1
          AND countryReference.IsDeleted = 0
        ORDER BY countryReference.Id
    ) AS country
    OUTER APPLY
    (
        SELECT TOP (1)
            itemsCategory.Id AS ItemsCategoryId
        FROM dbo.ItemsCategories AS itemsCategory
        WHERE itemsCategory.CompanyId = company.Id
          AND itemsCategory.IsActive = 1
          AND itemsCategory.IsDeleted = 0
        ORDER BY itemsCategory.Id
    ) AS category
    OUTER APPLY
    (
        SELECT TOP (1)
            customerStore.Id AS ContainerStoreId,
            assignment.ContainerId
        FROM dbo.Stores AS customerStore
        INNER JOIN dbo.StoreContainers AS assignment
            ON assignment.CompanyId = customerStore.CompanyId
           AND assignment.StoreId = customerStore.Id
           AND assignment.IsActive = 1
           AND assignment.IsDeleted = 0
        INNER JOIN dbo.Containers AS container
            ON container.CompanyId = assignment.CompanyId
           AND container.Id = assignment.ContainerId
           AND container.IsActive = 1
           AND container.IsDeleted = 0
        WHERE customerStore.CompanyId = company.Id
          AND customerStore.BusinessPartnerId = partner.BusinessPartnerId
          AND customerStore.IsContainerStore = 1
          AND customerStore.IsActive = 1
          AND customerStore.IsDeleted = 0
        ORDER BY customerStore.Id, assignment.ContainerId
    ) AS containerAssignment
    WHERE company.IsDeleted = 0;

    SELECT @PoolCount = COUNT(*)
    FROM #SeedPool;

    IF @PoolCount = 0
    BEGIN
        RAISERROR (N'No active company/partner/store/item combination was found. Nothing was inserted.', 16, 1);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM #SeedPool
        WHERE ContainerStoreId IS NOT NULL
          AND ContainerId IS NOT NULL
    )
    BEGIN
        RAISERROR (N'No active customer container store with an assigned container was found. The script cannot cover container invoices.', 16, 1);
    END;

    CREATE TABLE #GeneratedInvoices
    (
        InvoiceId int NOT NULL PRIMARY KEY,
        CompanyId int NOT NULL,
        InvoiceType int NOT NULL,
        PaymentTerm int NOT NULL,
        InvoiceDate date NOT NULL,
        Total decimal(18, 2) NOT NULL,
        PaidAmount decimal(18, 2) NOT NULL,
        HasSettlement bit NOT NULL,
        HasContainerLine bit NOT NULL
    );

    CREATE TABLE #GeneratedLines
    (
        InvoiceLineId int NOT NULL PRIMARY KEY,
        InvoiceId int NOT NULL,
        CompanyId int NOT NULL,
        BusinessPartnerId int NOT NULL,
        StoreId int NOT NULL,
        InvoiceType int NOT NULL,
        InvoiceDate date NOT NULL,
        ItemId int NOT NULL,
        ItemUnitId int NOT NULL,
        ItemName nvarchar(200) NOT NULL,
        Price decimal(18, 2) NOT NULL,
        SourceInvoiceLineId int NULL
    );

    DECLARE @Index int = 1;
    DECLARE @PoolIndex int;
    DECLARE @CompanyId int;
    DECLARE @BusinessPartnerId int;
    DECLARE @StoreId int;
    DECLARE @CountryId int;
    DECLARE @ItemsCategoryId int;
    DECLARE @ItemId int;
    DECLARE @ItemUnitId int;
    DECLARE @ItemName nvarchar(200);
    DECLARE @ContainerStoreId int;
    DECLARE @ContainerId int;
    DECLARE @InvoiceStoreId int;
    DECLARE @ContentType int;
    DECLARE @CashboxId int;
    DECLARE @CashMovementTypeId int;
    DECLARE @CashVoucherId int;
    DECLARE @CashDirection int;
    DECLARE @HasSettlement bit;
    DECLARE @HasContainerLine bit;
    DECLARE @InvoiceId int;
    DECLARE @InvoiceLineId int;
    DECLARE @InvoiceType int;
    DECLARE @SourceInvoiceType int;
    DECLARE @PaymentTerm int;
    DECLARE @InvoiceDate date;
    DECLARE @DueDate date;
    DECLARE @InvoiceNumber nvarchar(100);
    DECLARE @ExportInvoiceCode nvarchar(100);
    DECLARE @PartnerInvoiceNo nvarchar(100);
    DECLARE @InvoiceLineNotes nvarchar(1000);
    DECLARE @InvoiceNotes nvarchar(1000);
    DECLARE @SourceInvoiceLineId int;
    DECLARE @LineCount int;
    DECLARE @LineWeight decimal(18, 6);
    DECLARE @Quantity decimal(18, 6);
    DECLARE @Price decimal(18, 2);
    DECLARE @LineTotal decimal(18, 2);
    DECLARE @DiscountAmount decimal(18, 2);
    DECLARE @Total decimal(18, 2);
    DECLARE @PaidAmount decimal(18, 2);
    DECLARE @ReturnUnitCost decimal(24, 8);
    DECLARE @ErrorMessage nvarchar(2048);

    WHILE @Index <= @InvoiceCount
    BEGIN
        -- Reset nullable selections so a SELECT that finds no row cannot reuse
        -- a value from the previous iteration.
        SET @ContainerStoreId = NULL;
        SET @ContainerId = NULL;
        SET @CashboxId = NULL;
        SET @CashMovementTypeId = NULL;
        SET @CashVoucherId = NULL;
        SET @HasSettlement = 0;
        SET @HasContainerLine = 0;

        -- Pair each return row with the corresponding source row from the
        -- first 100 rows. This keeps company, partner and store distribution
        -- balanced, including when the pool count does not divide 100.
        SET @PoolIndex = CASE
            WHEN @Index > 100
                THEN ((@Index - 101) % @PoolCount) + 1
            ELSE ((@Index - 1) % @PoolCount) + 1
        END;

        SELECT
            @CompanyId = pool.CompanyId,
            @BusinessPartnerId = pool.BusinessPartnerId,
            @StoreId = pool.StoreId,
            @CountryId = pool.CountryId,
            @ItemsCategoryId = pool.ItemsCategoryId,
            @ItemId = pool.ItemId,
            @ItemUnitId = pool.ItemUnitId,
            @ItemName = pool.ItemName,
            @ContainerStoreId = pool.ContainerStoreId,
            @ContainerId = pool.ContainerId
        FROM #SeedPool AS pool
        WHERE pool.PoolIndex = @PoolIndex;

        -- First 100 rows are source invoices; last 100 rows are their returns.
        SET @InvoiceType = CASE
            WHEN @Index <= 100 AND @Index % 2 = 1 THEN 1       -- Sales
            WHEN @Index <= 100 THEN 2                          -- Purchase
            WHEN @Index % 2 = 1 THEN 3                         -- SalesReturn
            ELSE 4                                             -- PurchaseReturn
        END;

        -- Container coverage must not depend on which pool row happens to be
        -- selected at a given index. Eligible rows use a known customer store.
        IF @InvoiceType IN (1, 3) AND @Index % 10 = 1
        BEGIN
            SELECT TOP (1)
                @CompanyId = pool.CompanyId,
                @BusinessPartnerId = pool.BusinessPartnerId,
                @StoreId = pool.StoreId,
                @CountryId = pool.CountryId,
                @ItemsCategoryId = pool.ItemsCategoryId,
                @ItemId = pool.ItemId,
                @ItemUnitId = pool.ItemUnitId,
                @ItemName = pool.ItemName,
                @ContainerStoreId = pool.ContainerStoreId,
                @ContainerId = pool.ContainerId
            FROM #SeedPool AS pool
            WHERE pool.ContainerStoreId IS NOT NULL
              AND pool.ContainerId IS NOT NULL
            ORDER BY pool.PoolIndex;
        END;

        -- Odd rows are cash; even rows are credit.
        SET @PaymentTerm = CASE WHEN @Index % 2 = 1 THEN 1 ELSE 2 END;
        SET @InvoiceDate = DATEADD(DAY, -@InvoiceCount + @Index, @Today);
        SET @DueDate = CASE
            WHEN @PaymentTerm = 2 THEN DATEADD(DAY, 14, @InvoiceDate)
            ELSE NULL
        END;

        SET @SourceInvoiceLineId = NULL;
        SET @ReturnUnitCost = NULL;
        SET @LineCount = 1 + ((@Index - 1) % 5);
        SET @LineWeight = 1.000000;
        SET @Price = CONVERT(decimal(18, 2), 50 + ((@Index * 37) % 950));

        SET @HasContainerLine = CASE
            WHEN @InvoiceType IN (1, 3)
             AND @Index % 10 = 1
             AND @ContainerStoreId IS NOT NULL
             AND @ContainerId IS NOT NULL THEN 1
            ELSE 0
        END;

        SET @ContentType = CASE
            WHEN @HasContainerLine = 1 THEN 2 -- InvoiceContentType.Containers
            ELSE 1                             -- InvoiceContentType.Items
        END;
        SET @InvoiceStoreId = CASE
            WHEN @HasContainerLine = 1 THEN @ContainerStoreId
            ELSE @StoreId
        END;

        IF @InvoiceType IN (3, 4) AND @HasContainerLine = 0
        BEGIN
            SET @SourceInvoiceType = CASE WHEN @InvoiceType = 3 THEN 1 ELSE 2 END;

            SELECT TOP (1)
                @SourceInvoiceLineId = sourceLine.InvoiceLineId,
                @ItemId = sourceLine.ItemId,
                @ItemUnitId = sourceLine.ItemUnitId,
                @ItemName = sourceLine.ItemName,
                @Price = sourceLine.Price
            FROM #GeneratedLines AS sourceLine
            WHERE sourceLine.CompanyId = @CompanyId
              AND sourceLine.BusinessPartnerId = @BusinessPartnerId
              AND sourceLine.StoreId = @StoreId
              AND sourceLine.InvoiceType = @SourceInvoiceType
              AND sourceLine.InvoiceDate <= @InvoiceDate
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM #GeneratedLines AS usedSource
                  WHERE usedSource.SourceInvoiceLineId = sourceLine.InvoiceLineId
              )
            ORDER BY sourceLine.InvoiceDate, sourceLine.InvoiceLineId;

            IF @SourceInvoiceLineId IS NULL
            BEGIN
                SET @ErrorMessage = CONCAT(
                    N'Could not find an available source invoice line for return row ',
                    @Index,
                    N' in company ',
                    @CompanyId,
                    N'.');
                RAISERROR (@ErrorMessage, 16, 1);
            END;

            SET @LineCount = 1;
            SET @LineWeight = 1.000000;
            SET @ReturnUnitCost = CASE
                WHEN @InvoiceType = 3 THEN CONVERT(decimal(24, 8), @Price)
                ELSE NULL
            END;
        END;

        IF @HasContainerLine = 1
        BEGIN
            -- Container invoices do not have item lines, so their calculated
            -- invoice amount is zero in the domain model.
            SET @Quantity = 0.000000;
            SET @LineTotal = 0.00;
            SET @DiscountAmount = 0.00;
            SET @Total = 0.00;
            SET @PaidAmount = 0.00;
        END
        ELSE
        BEGIN
            SET @ContainerStoreId = NULL;
            SET @ContainerId = NULL;
            SET @Quantity = CONVERT(decimal(18, 6), @LineCount * @LineWeight);
            SET @LineTotal = CONVERT(decimal(18, 2), ROUND(@Quantity * @Price, 2));
            SET @DiscountAmount = CASE
                WHEN @Index % 7 = 0 THEN CONVERT(decimal(18, 2), ROUND(@LineTotal * 0.10, 2))
                ELSE CONVERT(decimal(18, 2), 0)
            END;
            SET @Total = CONVERT(decimal(18, 2), @LineTotal - @DiscountAmount);
            SET @PaidAmount = CASE
                WHEN @PaymentTerm = 1 THEN @Total
                WHEN @Index % 4 = 0 THEN CONVERT(decimal(18, 2), 0)
                ELSE CONVERT(decimal(18, 2), ROUND(@Total * 0.45, 2))
            END;
        END;

        -- Match InvoiceMovementRules: Sales and PurchaseReturn receive cash;
        -- Purchase and SalesReturn pay cash.
        SET @CashDirection = CASE
            WHEN @InvoiceType IN (1, 4) THEN 1 -- CashDirection.Receipt
            ELSE 2                              -- CashDirection.Payment
        END;

        IF @PaidAmount > 0
        BEGIN
            SELECT TOP (1)
                @CashMovementTypeId = movementType.Id
            FROM dbo.CashMovementTypes AS movementType
            WHERE movementType.CompanyId = @CompanyId
              AND movementType.Direction = @CashDirection
              AND movementType.PartnerEffect = CASE
                  WHEN @CashDirection = 1 THEN 2 -- Credit
                  ELSE 1                         -- Debit
              END
              AND movementType.IsActive = 1
              AND movementType.IsDeleted = 0
              AND
              (
                  (@InvoiceType = 1 AND movementType.IsDefaultForSales = 1)
                  OR (@InvoiceType = 2 AND movementType.IsDefaultForPurchase = 1)
                  OR (@InvoiceType = 3 AND movementType.IsDefaultForSalesReturn = 1)
                  OR (@InvoiceType = 4 AND movementType.IsDefaultForPurchaseReturn = 1)
              )
            ORDER BY movementType.Id;

            IF @CashMovementTypeId IS NOT NULL
            BEGIN
                SELECT TOP (1)
                    @CashboxId = cashbox.Id
                FROM dbo.Cashboxes AS cashbox WITH (UPDLOCK, HOLDLOCK)
                OUTER APPLY
                (
                    SELECT
                        COALESCE(SUM(CASE
                            WHEN voucher.Direction = 1 THEN voucher.Amount
                            ELSE -voucher.Amount
                        END), 0) AS VoucherNetAmount
                    FROM dbo.CashVouchers AS voucher
                    WHERE voucher.CompanyId = cashbox.CompanyId
                      AND voucher.CashboxId = cashbox.Id
                      AND voucher.IsDeleted = 0
                ) AS cashboxActivity
                WHERE cashbox.CompanyId = @CompanyId
                  AND cashbox.Currency = 1 -- CurrencyCode.EGP
                  AND cashbox.IsActive = 1
                  AND cashbox.IsDeleted = 0
                  AND
                  (
                      @CashDirection = 1
                      OR cashbox.OpeningBalance + cashboxActivity.VoucherNetAmount >= @PaidAmount
                  )
                ORDER BY cashbox.Id;
            END;
        END;

        SET @InvoiceNumber = CONCAT(
            @SeedPrefix,
            N'C',
            @CompanyId,
            N'-I',
            RIGHT(CONCAT(N'000', @Index), 3));
        SET @ExportInvoiceCode = @InvoiceNumber;
        SET @PartnerInvoiceNo = CASE
            WHEN @InvoiceType IN (2, 4) THEN CONCAT(N'PARTNER-', @Index)
            ELSE NULL
        END;
        SET @InvoiceNotes = CONCAT(
            N'SQL test seed invoice ',
            @Index,
            N' of ',
            @InvoiceCount,
            N'. Direct SQL seed.');
        SET @InvoiceLineNotes = CASE
            WHEN @HasContainerLine = 1 THEN N'SQL test seed customer-container movement line.'
            WHEN @InvoiceType IN (3, 4) THEN N'SQL test seed return line linked to an original invoice line.'
            ELSE N'SQL test seed item line.'
        END;

        IF @PaidAmount > 0
           AND (@CashboxId IS NULL OR @CashMovementTypeId IS NULL)
        BEGIN
            SET @ErrorMessage = CONCAT(
                N'No valid EGP cashbox/default movement type is available for paid invoice row ',
                @Index,
                N' in company ',
                @CompanyId,
                N'. The script will not create a paid invoice without its settlement rows.');
            RAISERROR (@ErrorMessage, 16, 1);
        END;

        INSERT INTO dbo.Invoices
        (
            CompanyId,
            InvoiceNumber,
            ExportInvoiceCode,
            PartnerInvoiceNo,
            InvoiceType,
            ContentType,
            PaymentTerm,
            InvoiceDate,
            DueDate,
            BusinessPartnerId,
            StoreId,
            ContainerStoreId,
            CountryId,
            ItemsCategoryId,
            Currency,
            ExchangeRateId,
            ExchangeRate,
            DriverId,
            ActualDriverName,
            UsesExternalDriver,
            ExternalDriverName,
            VehicleNumber,
            DiscountAmount,
            WBWeight,
            WBScaleDifference,
            WBDiscount,
            WBTotal,
            PaidAmount,
            Total,
            BaseSubtotal,
            BaseDiscountAmount,
            BaseTotal,
            BasePaidAmountAtInvoiceRate,
            Notes,
            LastModifiedAt,
            CreatedById,
            CreatedOn,
            CreatedByPc,
            UpdatedById,
            UpdatedOn,
            UpdatedByPc,
            DeletedById,
            DeletedOn,
            DeletedByPc,
            IsDeleted
        )
        VALUES
        (
            @CompanyId,
            @InvoiceNumber,
            @ExportInvoiceCode,
            @PartnerInvoiceNo,
            @InvoiceType,
            @ContentType,
            @PaymentTerm,
            @InvoiceDate,
            @DueDate,
            @BusinessPartnerId,
            @InvoiceStoreId,
            @ContainerStoreId,
            @CountryId,
            @ItemsCategoryId,
            1, -- CurrencyCode.EGP
            NULL,
            1.000000000000,
            NULL, -- DriverId
            NULL, -- ActualDriverName
            0,
            NULL,
            NULL,
            @DiscountAmount,
            0.000000,
            0.000000,
            0.000000,
            0.000000,
            @PaidAmount,
            @Total,
            CONVERT(decimal(28, 8), @LineTotal),
            CONVERT(decimal(28, 8), @DiscountAmount),
            CONVERT(decimal(28, 8), @Total),
            CONVERT(decimal(28, 8), @PaidAmount),
            @InvoiceNotes,
            @Now,
            @CreatedById,
            @Now,
            @CreatedByPc,
            NULL,
            NULL,
            NULL,
            NULL,
            NULL,
            NULL,
            0
        );

        SET @InvoiceId = CONVERT(int, SCOPE_IDENTITY());

        IF @Total > 0
        BEGIN
            -- InvoiceMovementRules.GetPartnerAmounts: Sales and
            -- PurchaseReturn debit the partner; the other types credit it.
            INSERT INTO dbo.BusinessPartnerMovements
            (
                CompanyId,
                BusinessPartnerId,
                InvoiceId,
                CashVoucherId,
                MovementType,
                MovementDate,
                Currency,
                Debit,
                Credit,
                ExchangeRate,
                BaseDebit,
                BaseCredit,
                Description,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @BusinessPartnerId,
                @InvoiceId,
                NULL,
                @InvoiceType, -- enum values align for the four invoice types
                @InvoiceDate,
                1, -- CurrencyCode.EGP
                CASE WHEN @InvoiceType IN (1, 4) THEN @Total ELSE 0.00 END,
                CASE WHEN @InvoiceType IN (2, 3) THEN @Total ELSE 0.00 END,
                1.000000000000,
                CONVERT(decimal(28, 8), CASE WHEN @InvoiceType IN (1, 4) THEN @Total ELSE 0.00 END),
                CONVERT(decimal(28, 8), CASE WHEN @InvoiceType IN (2, 3) THEN @Total ELSE 0.00 END),
                CONCAT(N'Invoice ', @InvoiceNumber),
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );
        END;

        IF @HasContainerLine = 0
        BEGIN
            INSERT INTO dbo.InvoiceLines
            (
                CompanyId,
                InvoiceId,
                ItemId,
                ItemName,
                ItemUnitId,
                SourceInvoiceLineId,
                ReturnUnitCost,
                [Count],
                Weight,
                Quantity,
                Price,
                Total,
                BaseUnitPrice,
                BaseTotal,
                Notes,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @InvoiceId,
                @ItemId,
                @ItemName,
                @ItemUnitId,
                @SourceInvoiceLineId,
                @ReturnUnitCost,
                @LineCount,
                @LineWeight,
                @Quantity,
                @Price,
                @LineTotal,
                CONVERT(decimal(24, 8), @Price),
                CONVERT(decimal(28, 8), @LineTotal),
                @InvoiceLineNotes,
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );

            SET @InvoiceLineId = CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.ItemMovements
            (
                CompanyId,
                StoreId,
                ItemId,
                ItemUnitId,
                MovementType,
                ReferenceId,
                ReferenceNumber,
                MovementDate,
                QuantityIn,
                QuantityOut,
                CostStatus,
                PendingCostQuantity,
                UnitCost,
                TotalCost,
                QuantityAfter,
                AverageCostAfter,
                InventoryValueAfter,
                Description,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @InvoiceStoreId,
                @ItemId,
                @ItemUnitId,
                CASE @InvoiceType
                    WHEN 1 THEN 1 -- ItemMovementType.Sales
                    WHEN 2 THEN 3 -- ItemMovementType.Purchase
                    WHEN 3 THEN 2 -- ItemMovementType.SalesReturn
                    WHEN 4 THEN 4 -- ItemMovementType.PurchaseReturn
                END,
                @InvoiceId,
                @InvoiceNumber,
                @InvoiceDate,
                CASE WHEN @InvoiceType IN (2, 3) THEN @Quantity ELSE 0.000000 END,
                CASE WHEN @InvoiceType IN (1, 4) THEN @Quantity ELSE 0.000000 END,
                1, -- InventoryCostStatus.Final: ItemMovement entity default
                0.000000,
                CONVERT(decimal(24, 8), @Price),
                CONVERT(decimal(28, 8), @LineTotal),
                CASE WHEN @InvoiceType IN (2, 3) THEN @Quantity ELSE 0.000000 END,
                CASE WHEN @InvoiceType IN (2, 3) THEN CONVERT(decimal(24, 8), @Price) ELSE CONVERT(decimal(24, 8), 0) END,
                CASE WHEN @InvoiceType IN (2, 3) THEN CONVERT(decimal(28, 8), @LineTotal) ELSE CONVERT(decimal(28, 8), 0) END,
                CONCAT(N'Invoice ', @InvoiceNumber),
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );
        END;

        IF @HasContainerLine = 1
        BEGIN
            INSERT INTO dbo.InvoiceContainerLines
            (
                CompanyId,
                InvoiceId,
                ContainerId,
                OutgoingUnits,
                IncomingUnits,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @InvoiceId,
                @ContainerId,
                CASE WHEN @InvoiceType = 1 THEN 1 ELSE 0 END,
                CASE WHEN @InvoiceType = 3 THEN 1 ELSE 0 END,
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );

            INSERT INTO dbo.ContainerMovements
            (
                CompanyId,
                BusinessPartnerId,
                ContainerStoreId,
                ContainerId,
                InvoiceId,
                InvoiceNumber,
                MovementDate,
                OutgoingUnits,
                IncomingUnits,
                Description,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @BusinessPartnerId,
                @ContainerStoreId,
                @ContainerId,
                @InvoiceId,
                @InvoiceNumber,
                @InvoiceDate,
                CASE WHEN @InvoiceType = 1 THEN 1 ELSE 0 END,
                CASE WHEN @InvoiceType = 3 THEN 1 ELSE 0 END,
                CONCAT(N'Invoice ', @InvoiceNumber),
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );
        END;

        IF @PaidAmount > 0
           AND @CashboxId IS NOT NULL
           AND @CashMovementTypeId IS NOT NULL
        BEGIN
            INSERT INTO dbo.CashVouchers
            (
                CompanyId,
                InvoiceId,
                CashboxTransferId,
                VoucherNumber,
                VoucherDate,
                Direction,
                CashboxId,
                CashMovementTypeId,
                PartyType,
                BusinessPartnerId,
                DriverId,
                DriverTripId,
                ExternalPartyName,
                Amount,
                Currency,
                ExchangeRateId,
                ExchangeRate,
                BaseAmount,
                ReferenceNumber,
                Description,
                Notes,
                LastModifiedAt,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @InvoiceId,
                NULL,
                CONCAT(@SeedPrefix, N'CV-C', @CompanyId, N'-I', RIGHT(CONCAT(N'000', @Index), 3)),
                @InvoiceDate,
                @CashDirection,
                @CashboxId,
                @CashMovementTypeId,
                2, -- CashPartyType.Partner
                @BusinessPartnerId,
                NULL,
                NULL,
                NULL,
                @PaidAmount,
                1, -- CurrencyCode.EGP
                NULL,
                1.000000000000,
                CONVERT(decimal(28, 8), @PaidAmount),
                @InvoiceNumber,
                CONCAT(N'SQL seed payment for invoice ', @InvoiceNumber),
                N'Direct SQL seed: paired with InvoicePayments and partner movements.',
                @Now,
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );

            SET @CashVoucherId = CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.BusinessPartnerMovements
            (
                CompanyId,
                BusinessPartnerId,
                InvoiceId,
                CashVoucherId,
                MovementType,
                MovementDate,
                Currency,
                Debit,
                Credit,
                ExchangeRate,
                BaseDebit,
                BaseCredit,
                Description,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @BusinessPartnerId,
                NULL,
                @CashVoucherId,
                CASE WHEN @CashDirection = 1 THEN 5 ELSE 6 END,
                @InvoiceDate,
                1, -- CurrencyCode.EGP
                CASE WHEN @CashDirection = 2 THEN @PaidAmount ELSE 0.00 END,
                CASE WHEN @CashDirection = 1 THEN @PaidAmount ELSE 0.00 END,
                1.000000000000,
                CONVERT(decimal(28, 8), CASE WHEN @CashDirection = 2 THEN @PaidAmount ELSE 0.00 END),
                CONVERT(decimal(28, 8), CASE WHEN @CashDirection = 1 THEN @PaidAmount ELSE 0.00 END),
                CONCAT(N'SQL seed payment for invoice ', @InvoiceNumber),
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );

            INSERT INTO dbo.InvoicePayments
            (
                CompanyId,
                InvoiceId,
                CashVoucherId,
                InvoiceCurrency,
                AppliedAmount,
                CashboxCurrency,
                CashboxAmount,
                InvoiceToBaseRate,
                CashboxToBaseRate,
                AppliedBaseAmount,
                CashboxBaseAmount,
                RealizedExchangeDifference,
                CreatedById,
                CreatedOn,
                CreatedByPc,
                UpdatedById,
                UpdatedOn,
                UpdatedByPc,
                DeletedById,
                DeletedOn,
                DeletedByPc,
                IsDeleted
            )
            VALUES
            (
                @CompanyId,
                @InvoiceId,
                @CashVoucherId,
                1, -- CurrencyCode.EGP
                @PaidAmount,
                1, -- CurrencyCode.EGP
                @PaidAmount,
                1.000000000000,
                1.000000000000,
                CONVERT(decimal(28, 8), @PaidAmount),
                CONVERT(decimal(28, 8), @PaidAmount),
                CONVERT(decimal(28, 8), 0),
                @CreatedById,
                @Now,
                @CreatedByPc,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0
            );

            SET @HasSettlement = 1;
        END;

        INSERT INTO #GeneratedInvoices
        (
            InvoiceId,
            CompanyId,
            InvoiceType,
            PaymentTerm,
            InvoiceDate,
            Total,
            PaidAmount,
            HasSettlement,
            HasContainerLine
        )
        VALUES
        (
            @InvoiceId,
            @CompanyId,
            @InvoiceType,
            @PaymentTerm,
            @InvoiceDate,
            @Total,
            @PaidAmount,
            @HasSettlement,
            @HasContainerLine
        );

        IF @HasContainerLine = 0
        BEGIN
            INSERT INTO #GeneratedLines
            (
                InvoiceLineId,
                InvoiceId,
                CompanyId,
                BusinessPartnerId,
                StoreId,
                InvoiceType,
                InvoiceDate,
                ItemId,
                ItemUnitId,
                ItemName,
                Price,
                SourceInvoiceLineId
            )
            VALUES
            (
                @InvoiceLineId,
                @InvoiceId,
                @CompanyId,
                @BusinessPartnerId,
                @InvoiceStoreId,
                @InvoiceType,
                @InvoiceDate,
                @ItemId,
                @ItemUnitId,
                @ItemName,
                @Price,
                @SourceInvoiceLineId
            );
        END;

        SET @Index += 1;
    END;

    IF (SELECT COUNT(*) FROM #GeneratedInvoices) <> @InvoiceCount
    BEGIN
        RAISERROR (N'The generated invoice count is not 200. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT InvoiceType
        FROM #GeneratedInvoices
        GROUP BY InvoiceType
        HAVING COUNT(*) <> 50
    )
    BEGIN
        RAISERROR (N'The generated invoice types are not balanced at 50 rows each. The transaction will be rolled back.', 16, 1);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM #GeneratedInvoices
        WHERE HasContainerLine = 1
    )
    BEGIN
        RAISERROR (N'No customer-container invoice was generated. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedInvoices
        WHERE PaidAmount > 0
          AND HasSettlement = 0
    )
    BEGIN
        RAISERROR (N'A paid invoice has no settlement rows. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedLines AS line
        LEFT JOIN dbo.ItemMovements AS movement
            ON movement.CompanyId = line.CompanyId
           AND movement.ReferenceId = line.InvoiceId
           AND movement.ItemId = line.ItemId
           AND movement.IsDeleted = 0
        WHERE movement.Id IS NULL
           OR movement.StoreId <> line.StoreId
           OR movement.ReferenceNumber <> (
                SELECT invoice.InvoiceNumber
                FROM dbo.Invoices AS invoice
                WHERE invoice.CompanyId = line.CompanyId
                  AND invoice.Id = line.InvoiceId)
           OR movement.MovementType <> CASE line.InvoiceType
                WHEN 1 THEN 1
                WHEN 2 THEN 3
                WHEN 3 THEN 2
                WHEN 4 THEN 4
              END
           OR movement.QuantityIn <> CASE WHEN line.InvoiceType IN (2, 3)
                THEN CONVERT(decimal(18, 6), 1) * (
                    SELECT invoiceLine.Quantity
                    FROM dbo.InvoiceLines AS invoiceLine
                    WHERE invoiceLine.CompanyId = line.CompanyId
                      AND invoiceLine.Id = line.InvoiceLineId)
                ELSE 0
              END
           OR movement.QuantityOut <> CASE WHEN line.InvoiceType IN (1, 4)
                THEN CONVERT(decimal(18, 6), 1) * (
                    SELECT invoiceLine.Quantity
                    FROM dbo.InvoiceLines AS invoiceLine
                    WHERE invoiceLine.CompanyId = line.CompanyId
                      AND invoiceLine.Id = line.InvoiceLineId)
                ELSE 0
              END
    )
    BEGIN
        RAISERROR (N'An item invoice line is missing its correctly directed ItemMovement. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedInvoices AS generated
        WHERE generated.Total > 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.BusinessPartnerMovements AS movement
              WHERE movement.CompanyId = generated.CompanyId
                AND movement.InvoiceId = generated.InvoiceId
                AND movement.CashVoucherId IS NULL
                AND movement.MovementType = generated.InvoiceType
                AND movement.Currency = 1
                AND movement.Debit = CASE WHEN generated.InvoiceType IN (1, 4)
                    THEN generated.Total ELSE 0 END
                AND movement.Credit = CASE WHEN generated.InvoiceType IN (2, 3)
                    THEN generated.Total ELSE 0 END
                AND movement.BaseDebit = CASE WHEN generated.InvoiceType IN (1, 4)
                    THEN CONVERT(decimal(28, 8), generated.Total) ELSE 0 END
                AND movement.BaseCredit = CASE WHEN generated.InvoiceType IN (2, 3)
                    THEN CONVERT(decimal(28, 8), generated.Total) ELSE 0 END
                AND movement.IsDeleted = 0
          )
    )
    BEGIN
        RAISERROR (N'An invoice is missing its correctly directed BusinessPartnerMovement. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedInvoices AS generated
        INNER JOIN dbo.CashVouchers AS voucher
            ON voucher.CompanyId = generated.CompanyId
           AND voucher.InvoiceId = generated.InvoiceId
           AND voucher.IsDeleted = 0
        WHERE generated.PaidAmount > 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.BusinessPartnerMovements AS movement
              WHERE movement.CompanyId = generated.CompanyId
                AND movement.InvoiceId IS NULL
                AND movement.CashVoucherId = voucher.Id
                AND movement.MovementType = CASE WHEN generated.InvoiceType IN (1, 4) THEN 5 ELSE 6 END
                AND movement.Currency = 1
                AND movement.Debit = CASE WHEN generated.InvoiceType IN (2, 3)
                    THEN generated.PaidAmount ELSE 0 END
                AND movement.Credit = CASE WHEN generated.InvoiceType IN (1, 4)
                    THEN generated.PaidAmount ELSE 0 END
                AND movement.BaseDebit = CASE WHEN generated.InvoiceType IN (2, 3)
                    THEN CONVERT(decimal(28, 8), generated.PaidAmount) ELSE 0 END
                AND movement.BaseCredit = CASE WHEN generated.InvoiceType IN (1, 4)
                    THEN CONVERT(decimal(28, 8), generated.PaidAmount) ELSE 0 END
                AND movement.IsDeleted = 0
          )
    )
    BEGIN
        RAISERROR (N'A payment voucher is missing its correctly directed BusinessPartnerMovement. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedInvoices AS generated
        INNER JOIN dbo.InvoiceContainerLines AS line
            ON line.CompanyId = generated.CompanyId
           AND line.InvoiceId = generated.InvoiceId
           AND line.IsDeleted = 0
        LEFT JOIN dbo.ContainerMovements AS movement
            ON movement.CompanyId = generated.CompanyId
           AND movement.InvoiceId = generated.InvoiceId
           AND movement.ContainerId = line.ContainerId
           AND movement.IsDeleted = 0
        WHERE generated.HasContainerLine = 1
          AND
          (
              movement.Id IS NULL
              OR movement.OutgoingUnits <> line.OutgoingUnits
              OR movement.IncomingUnits <> line.IncomingUnits
          )
    )
    BEGIN
        RAISERROR (N'An InvoiceContainerLine is missing its matching ContainerMovement. The transaction will be rolled back.', 16, 1);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM #GeneratedLines
        WHERE InvoiceType IN (3, 4)
          AND SourceInvoiceLineId IS NULL
    )
    BEGIN
        RAISERROR (N'An item return is not linked to a source invoice line. The transaction will be rolled back.', 16, 1);
    END;

    COMMIT TRANSACTION;

    SELECT
        COUNT(*) AS InsertedInvoices,
        (SELECT COUNT(*) FROM #GeneratedLines) AS InsertedItemLines,
        SUM(CASE WHEN HasContainerLine = 1 THEN 1 ELSE 0 END) AS InsertedContainerLines,
        SUM(CASE WHEN HasSettlement = 1 THEN 1 ELSE 0 END) AS InsertedSettlements,
        (SELECT COUNT(*)
         FROM dbo.ItemMovements AS movement
         INNER JOIN #GeneratedInvoices AS generated
             ON generated.CompanyId = movement.CompanyId
            AND generated.InvoiceId = movement.ReferenceId
         WHERE movement.MovementType IN (1, 2, 3, 4)
           AND movement.IsDeleted = 0) AS InsertedItemMovements,
        (SELECT COUNT(*)
         FROM dbo.BusinessPartnerMovements AS movement
         WHERE movement.IsDeleted = 0
           AND
           (
               EXISTS
               (
                   SELECT 1
                   FROM #GeneratedInvoices AS generated
                   WHERE generated.CompanyId = movement.CompanyId
                     AND generated.InvoiceId = movement.InvoiceId
               )
               OR EXISTS
               (
                   SELECT 1
                   FROM dbo.CashVouchers AS voucher
                   INNER JOIN #GeneratedInvoices AS generated
                       ON generated.CompanyId = voucher.CompanyId
                      AND generated.InvoiceId = voucher.InvoiceId
                   WHERE voucher.CompanyId = movement.CompanyId
                     AND voucher.Id = movement.CashVoucherId
               )
           )) AS InsertedBusinessPartnerMovements,
        (SELECT COUNT(*)
         FROM dbo.ContainerMovements AS movement
         INNER JOIN #GeneratedInvoices AS generated
             ON generated.CompanyId = movement.CompanyId
            AND generated.InvoiceId = movement.InvoiceId
         WHERE movement.IsDeleted = 0) AS InsertedContainerMovements
    FROM #GeneratedInvoices;

    SELECT
        InvoiceType,
        CASE InvoiceType
            WHEN 1 THEN N'Sales'
            WHEN 2 THEN N'Purchase'
            WHEN 3 THEN N'SalesReturn'
            WHEN 4 THEN N'PurchaseReturn'
        END AS InvoiceTypeName,
        PaymentTerm,
        CASE PaymentTerm
            WHEN 1 THEN N'Cash'
            WHEN 2 THEN N'Credit'
        END AS PaymentTermName,
        CASE WHEN HasContainerLine = 1 THEN N'Containers' ELSE N'Items' END AS ContentTypeName,
        CASE
            WHEN PaidAmount <= 0 AND Total > 0 THEN N'Unpaid'
            WHEN Total - PaidAmount <= 0 THEN N'Paid'
            ELSE N'PartiallyPaid'
        END AS PaymentStatus,
        COUNT(*) AS InvoiceCount
    FROM #GeneratedInvoices
    GROUP BY
        InvoiceType,
        PaymentTerm,
        HasContainerLine,
        CASE
            WHEN PaidAmount <= 0 AND Total > 0 THEN N'Unpaid'
            WHEN Total - PaidAmount <= 0 THEN N'Paid'
            ELSE N'PartiallyPaid'
        END
    ORDER BY InvoiceType, PaymentTerm, PaymentStatus;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    DECLARE @CatchMessage nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR (@CatchMessage, 16, 1);
END CATCH;
