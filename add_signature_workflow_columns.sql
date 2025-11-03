-- Migration: Add Signature Workflow Columns to DocumentSignatures table
-- Date: 2025-11-03
-- Description: Adds columns required for electronic signature workflow

USE CoOwnershipVehicle_Group;
GO

-- Check if columns already exist before adding them
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'SigningToken')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [SigningToken] NVARCHAR(500) NULL;

    PRINT 'Added SigningToken column';
END
ELSE
BEGIN
    PRINT 'SigningToken column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'TokenExpiresAt')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [TokenExpiresAt] DATETIME2 NULL;

    PRINT 'Added TokenExpiresAt column';
END
ELSE
BEGIN
    PRINT 'TokenExpiresAt column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'DueDate')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [DueDate] DATETIME2 NULL;

    PRINT 'Added DueDate column';
END
ELSE
BEGIN
    PRINT 'DueDate column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'Message')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [Message] NVARCHAR(1000) NULL;

    PRINT 'Added Message column';
END
ELSE
BEGIN
    PRINT 'Message column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'SigningMode')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [SigningMode] INT NOT NULL DEFAULT 0;

    PRINT 'Added SigningMode column (0=Parallel, 1=Sequential)';
END
ELSE
BEGIN
    PRINT 'SigningMode column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'IsNotificationSent')
BEGIN
    ALTER TABLE [dbo].[DocumentSignatures]
    ADD [IsNotificationSent] BIT NOT NULL DEFAULT 0;

    PRINT 'Added IsNotificationSent column';
END
ELSE
BEGIN
    PRINT 'IsNotificationSent column already exists';
END
GO

-- Add UploadedBy column to Documents table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Documents]') AND name = 'UploadedBy')
BEGIN
    ALTER TABLE [dbo].[Documents]
    ADD [UploadedBy] UNIQUEIDENTIFIER NULL;

    PRINT 'Added UploadedBy column to Documents table';
END
ELSE
BEGIN
    PRINT 'UploadedBy column already exists in Documents table';
END
GO

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'IX_DocumentSignatures_SigningToken')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DocumentSignatures_SigningToken]
    ON [dbo].[DocumentSignatures]([SigningToken])
    WHERE [SigningToken] IS NOT NULL;

    PRINT 'Created index on SigningToken';
END
ELSE
BEGIN
    PRINT 'Index on SigningToken already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'IX_DocumentSignatures_TokenExpiresAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DocumentSignatures_TokenExpiresAt]
    ON [dbo].[DocumentSignatures]([TokenExpiresAt])
    WHERE [TokenExpiresAt] IS NOT NULL;

    PRINT 'Created index on TokenExpiresAt';
END
ELSE
BEGIN
    PRINT 'Index on TokenExpiresAt already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DocumentSignatures]') AND name = 'IX_DocumentSignatures_DueDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DocumentSignatures_DueDate]
    ON [dbo].[DocumentSignatures]([DueDate])
    WHERE [DueDate] IS NOT NULL;

    PRINT 'Created index on DueDate';
END
ELSE
BEGIN
    PRINT 'Index on DueDate already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Documents]') AND name = 'IX_Documents_UploadedBy')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Documents_UploadedBy]
    ON [dbo].[Documents]([UploadedBy])
    WHERE [UploadedBy] IS NOT NULL;

    PRINT 'Created index on UploadedBy';
END
ELSE
BEGIN
    PRINT 'Index on UploadedBy already exists';
END
GO

-- Verify the changes
SELECT
    'DocumentSignatures' AS TableName,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DocumentSignatures'
    AND COLUMN_NAME IN ('SigningToken', 'TokenExpiresAt', 'DueDate', 'Message', 'SigningMode', 'IsNotificationSent')
ORDER BY COLUMN_NAME;
GO

SELECT
    'Documents' AS TableName,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Documents'
    AND COLUMN_NAME = 'UploadedBy';
GO

PRINT 'Migration completed successfully!';
GO
