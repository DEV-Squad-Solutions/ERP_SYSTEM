/*
    Development database repair for:
      20260816113257_modifyActualDriverId
      20260816120443_RenameActualDriverIdToActualDriverName

    This script supports all three expected states:
      - ActualDriverId is still int.
      - ActualDriverId is already nvarchar(200).
      - ActualDriverName already exists.

    Stop the API before running it so EF migrations cannot run concurrently.
    Select the application database in SSMS; never run it against master.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @InitialMigrationId nvarchar(150) =
    N'20260815205829_CreateDatabase';
DECLARE @ConversionMigrationId nvarchar(150) =
    N'20260816113257_modifyActualDriverId';
DECLARE @RenameMigrationId nvarchar(150) =
    N'20260816120443_RenameActualDriverIdToActualDriverName';
DECLARE @ProductVersion nvarchar(32) = N'10.0.10';

IF DB_NAME() = N'master'
BEGIN
    THROW 51000, 'Select the application database before running this script.', 1;
END;

IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
   OR OBJECT_ID(N'[dbo].[DriverTrips]', N'U') IS NULL
BEGIN
    THROW 51001, 'Invoices or DriverTrips table was not found.', 1;
END;

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 51002, 'EF migrations history was not found.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = @InitialMigrationId
)
BEGIN
    THROW 51003, 'The initial migration is not recorded; recreate the Development database instead.', 1;
END;

DECLARE @InvoicesHasOldColumn bit =
    CASE WHEN COL_LENGTH(N'dbo.Invoices', N'ActualDriverId') IS NULL
        THEN 0 ELSE 1 END;
DECLARE @InvoicesHasNewColumn bit =
    CASE WHEN COL_LENGTH(N'dbo.Invoices', N'ActualDriverName') IS NULL
        THEN 0 ELSE 1 END;
DECLARE @DriverTripsHasOldColumn bit =
    CASE WHEN COL_LENGTH(N'dbo.DriverTrips', N'ActualDriverId') IS NULL
        THEN 0 ELSE 1 END;
DECLARE @DriverTripsHasNewColumn bit =
    CASE WHEN COL_LENGTH(N'dbo.DriverTrips', N'ActualDriverName') IS NULL
        THEN 0 ELSE 1 END;

IF @InvoicesHasOldColumn = @InvoicesHasNewColumn
BEGIN
    THROW 51004, 'Invoices must contain exactly one of ActualDriverId or ActualDriverName.', 1;
END;

IF @DriverTripsHasOldColumn = @DriverTripsHasNewColumn
BEGIN
    THROW 51005, 'DriverTrips must contain exactly one of ActualDriverId or ActualDriverName.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] =
            N'FK_DriverTrips_Drivers_CompanyId_ActualDriverId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[DriverTrips]')
    )
    BEGIN
        ALTER TABLE [dbo].[DriverTrips]
            DROP CONSTRAINT
                [FK_DriverTrips_Drivers_CompanyId_ActualDriverId];
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] =
            N'FK_Invoices_Drivers_CompanyId_ActualDriverId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[Invoices]')
    )
    BEGIN
        ALTER TABLE [dbo].[Invoices]
            DROP CONSTRAINT
                [FK_Invoices_Drivers_CompanyId_ActualDriverId];
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_Invoices_CompanyId_ActualDriverId'
          AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]')
    )
    BEGIN
        DROP INDEX [IX_Invoices_CompanyId_ActualDriverId]
            ON [dbo].[Invoices];
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_DriverTrips_CompanyId_ActualDriverId'
          AND [object_id] = OBJECT_ID(N'[dbo].[DriverTrips]')
    )
    BEGIN
        DROP INDEX [IX_DriverTrips_CompanyId_ActualDriverId]
            ON [dbo].[DriverTrips];
    END;

    DECLARE @InvoicesDefaultConstraint sysname;
    SELECT @InvoicesDefaultConstraint = defaultConstraint.[name]
    FROM sys.default_constraints AS defaultConstraint
    INNER JOIN sys.columns AS columnDefinition
        ON columnDefinition.[object_id] = defaultConstraint.[parent_object_id]
       AND columnDefinition.[column_id] = defaultConstraint.[parent_column_id]
    WHERE defaultConstraint.[parent_object_id] =
            OBJECT_ID(N'[dbo].[Invoices]')
      AND columnDefinition.[name] IN (N'ActualDriverId', N'ActualDriverName');

    IF @InvoicesDefaultConstraint IS NOT NULL
    BEGIN
        EXEC
        (
            N'ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT ' +
            QUOTENAME(@InvoicesDefaultConstraint) + N';'
        );
    END;

    DECLARE @DriverTripsDefaultConstraint sysname;
    SELECT @DriverTripsDefaultConstraint = defaultConstraint.[name]
    FROM sys.default_constraints AS defaultConstraint
    INNER JOIN sys.columns AS columnDefinition
        ON columnDefinition.[object_id] = defaultConstraint.[parent_object_id]
       AND columnDefinition.[column_id] = defaultConstraint.[parent_column_id]
    WHERE defaultConstraint.[parent_object_id] =
            OBJECT_ID(N'[dbo].[DriverTrips]')
      AND columnDefinition.[name] IN (N'ActualDriverId', N'ActualDriverName');

    IF @DriverTripsDefaultConstraint IS NOT NULL
    BEGIN
        EXEC
        (
            N'ALTER TABLE [dbo].[DriverTrips] DROP CONSTRAINT ' +
            QUOTENAME(@DriverTripsDefaultConstraint) + N';'
        );
    END;

    IF @InvoicesHasOldColumn = 1
    BEGIN
        EXEC sys.sp_rename
            N'dbo.Invoices.ActualDriverId',
            N'ActualDriverName',
            N'COLUMN';
    END;

    IF @DriverTripsHasOldColumn = 1
    BEGIN
        EXEC sys.sp_rename
            N'dbo.DriverTrips.ActualDriverId',
            N'ActualDriverName',
            N'COLUMN';
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS columnDefinition
        INNER JOIN sys.types AS typeDefinition
            ON typeDefinition.[user_type_id] = columnDefinition.[user_type_id]
        WHERE columnDefinition.[object_id] = OBJECT_ID(N'[dbo].[Invoices]')
          AND columnDefinition.[name] = N'ActualDriverName'
          AND
          (
              typeDefinition.[name] <> N'nvarchar'
              OR columnDefinition.[max_length] <> 400
              OR columnDefinition.[is_nullable] <> 1
          )
    )
    BEGIN
        ALTER TABLE [dbo].[Invoices]
            ALTER COLUMN [ActualDriverName] nvarchar(200) NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS columnDefinition
        INNER JOIN sys.types AS typeDefinition
            ON typeDefinition.[user_type_id] = columnDefinition.[user_type_id]
        WHERE columnDefinition.[object_id] = OBJECT_ID(N'[dbo].[DriverTrips]')
          AND columnDefinition.[name] = N'ActualDriverName'
          AND
          (
              typeDefinition.[name] <> N'nvarchar'
              OR columnDefinition.[max_length] <> 400
              OR columnDefinition.[is_nullable] <> 1
          )
    )
    BEGIN
        ALTER TABLE [dbo].[DriverTrips]
            ALTER COLUMN [ActualDriverName] nvarchar(200) NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns AS columnDefinition
        INNER JOIN sys.types AS typeDefinition
            ON typeDefinition.[user_type_id] = columnDefinition.[user_type_id]
        WHERE columnDefinition.[object_id] = OBJECT_ID(N'[dbo].[Invoices]')
          AND columnDefinition.[name] = N'ActualDriverName'
          AND typeDefinition.[name] = N'nvarchar'
          AND columnDefinition.[max_length] = 400
          AND columnDefinition.[is_nullable] = 1
    )
    BEGIN
        THROW 51006, 'Invoices.ActualDriverName was not converted correctly.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns AS columnDefinition
        INNER JOIN sys.types AS typeDefinition
            ON typeDefinition.[user_type_id] = columnDefinition.[user_type_id]
        WHERE columnDefinition.[object_id] = OBJECT_ID(N'[dbo].[DriverTrips]')
          AND columnDefinition.[name] = N'ActualDriverName'
          AND typeDefinition.[name] = N'nvarchar'
          AND columnDefinition.[max_length] = 400
          AND columnDefinition.[is_nullable] = 1
    )
    BEGIN
        THROW 51007, 'DriverTrips.ActualDriverName was not converted correctly.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = @ConversionMigrationId
    )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory]
            ([MigrationId], [ProductVersion])
        VALUES
            (@ConversionMigrationId, @ProductVersion);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = @RenameMigrationId
    )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory]
            ([MigrationId], [ProductVersion])
        VALUES
            (@RenameMigrationId, @ProductVersion);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;

SELECT
    DB_NAME() AS [DatabaseName],
    tableDefinition.[name] AS [TableName],
    columnDefinition.[name] AS [ColumnName],
    typeDefinition.[name] AS [DataType],
    columnDefinition.[max_length] / 2 AS [MaximumCharacters],
    columnDefinition.[is_nullable] AS [IsNullable]
FROM sys.columns AS columnDefinition
INNER JOIN sys.tables AS tableDefinition
    ON tableDefinition.[object_id] = columnDefinition.[object_id]
INNER JOIN sys.schemas AS schemaDefinition
    ON schemaDefinition.[schema_id] = tableDefinition.[schema_id]
INNER JOIN sys.types AS typeDefinition
    ON typeDefinition.[user_type_id] = columnDefinition.[user_type_id]
WHERE schemaDefinition.[name] = N'dbo'
  AND tableDefinition.[name] IN (N'Invoices', N'DriverTrips')
  AND columnDefinition.[name] = N'ActualDriverName'
ORDER BY tableDefinition.[name];

SELECT
    [MigrationId],
    [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
WHERE [MigrationId] IN (@ConversionMigrationId, @RenameMigrationId)
ORDER BY [MigrationId];
