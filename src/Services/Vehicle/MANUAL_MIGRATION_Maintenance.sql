-- Manual Migration Script for Maintenance Tables
-- Run this script manually in SQL Server Management Studio or Azure Data Studio
-- Database: CoOwnershipVehicle_Vehicle

USE [CoOwnershipVehicle_Vehicle];
GO

-- Check if tables already exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MaintenanceSchedules')
BEGIN
    PRINT 'Creating MaintenanceSchedules table...';

    CREATE TABLE [dbo].[MaintenanceSchedules] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [VehicleId] UNIQUEIDENTIFIER NOT NULL,
        [ServiceType] INT NOT NULL,
        [ScheduledDate] DATETIME2 NOT NULL,
        [Status] INT NOT NULL,
        [EstimatedCost] DECIMAL(18,2) NULL,
        [EstimatedDuration] INT NOT NULL,
        [ServiceProvider] NVARCHAR(200) NULL,
        [Notes] NVARCHAR(1000) NULL,
        [Priority] INT NOT NULL,
        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_MaintenanceSchedules_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_MaintenanceSchedules_VehicleId] ON [MaintenanceSchedules]([VehicleId]);
    CREATE INDEX [IX_MaintenanceSchedules_ScheduledDate] ON [MaintenanceSchedules]([ScheduledDate]);
    CREATE INDEX [IX_MaintenanceSchedules_Status] ON [MaintenanceSchedules]([Status]);

    PRINT 'MaintenanceSchedules table created successfully.';
END
ELSE
BEGIN
    PRINT 'MaintenanceSchedules table already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MaintenanceRecords')
BEGIN
    PRINT 'Creating MaintenanceRecords table...';

    CREATE TABLE [dbo].[MaintenanceRecords] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [VehicleId] UNIQUEIDENTIFIER NOT NULL,
        [ServiceType] INT NOT NULL,
        [ServiceDate] DATETIME2 NOT NULL,
        [OdometerReading] INT NOT NULL,
        [ActualCost] DECIMAL(18,2) NOT NULL,
        [ServiceProvider] NVARCHAR(200) NOT NULL,
        [WorkPerformed] NVARCHAR(2000) NOT NULL,
        [PartsReplaced] NVARCHAR(1000) NULL,
        [NextServiceDue] DATETIME2 NULL,
        [NextServiceOdometer] INT NULL,
        [ExpenseId] UNIQUEIDENTIFIER NULL,
        [PerformedBy] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_MaintenanceRecords_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_MaintenanceRecords_VehicleId] ON [MaintenanceRecords]([VehicleId]);
    CREATE INDEX [IX_MaintenanceRecords_ServiceDate] ON [MaintenanceRecords]([ServiceDate]);
    CREATE INDEX [IX_MaintenanceRecords_OdometerReading] ON [MaintenanceRecords]([OdometerReading]);

    PRINT 'MaintenanceRecords table created successfully.';
END
ELSE
BEGIN
    PRINT 'MaintenanceRecords table already exists.';
END
GO

-- Mark migrations as applied in EF Migrations History
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251026164739_InitialVehicleSchema')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251026164739_InitialVehicleSchema', N'8.0.0');
    PRINT 'Marked InitialVehicleSchema as applied.';
END
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251028130857_AddMaintenanceEntities')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251028130857_AddMaintenanceEntities', N'8.0.0');
    PRINT 'Marked AddMaintenanceEntities as applied.';
END
GO

PRINT '';
PRINT '========================================';
PRINT 'Migration completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'Tables created:';
PRINT '  - MaintenanceSchedules';
PRINT '  - MaintenanceRecords';
PRINT '';
PRINT 'You can now use the Maintenance API endpoints.';
GO
