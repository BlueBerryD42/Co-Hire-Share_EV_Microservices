-- Migration: Add SigningCertificates table
-- Date: 2025-11-03
-- Description: Creates table to store signing certificates for verification

USE CoOwnershipVehicle_Group;
GO

-- Create SigningCertificates table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SigningCertificates]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SigningCertificates](
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [DocumentId] UNIQUEIDENTIFIER NOT NULL,
        [CertificateId] NVARCHAR(100) NOT NULL,
        [DocumentHash] NVARCHAR(500) NOT NULL,
        [FileName] NVARCHAR(200) NOT NULL,
        [TotalSigners] INT NOT NULL,
        [GeneratedAt] DATETIME2 NOT NULL,
        [ExpiresAt] DATETIME2 NULL,
        [SignersJson] NVARCHAR(4000) NULL,
        [IsRevoked] BIT NOT NULL DEFAULT 0,
        [RevokedAt] DATETIME2 NULL,
        [RevocationReason] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_SigningCertificates_Documents] FOREIGN KEY ([DocumentId])
            REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE
    );

    PRINT 'Created SigningCertificates table';
END
ELSE
BEGIN
    PRINT 'SigningCertificates table already exists';
END
GO

-- Create unique index on CertificateId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SigningCertificates]') AND name = 'IX_SigningCertificates_CertificateId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_SigningCertificates_CertificateId]
    ON [dbo].[SigningCertificates]([CertificateId]);

    PRINT 'Created index on CertificateId';
END
GO

-- Create unique index on DocumentId (one certificate per document)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SigningCertificates]') AND name = 'IX_SigningCertificates_DocumentId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_SigningCertificates_DocumentId]
    ON [dbo].[SigningCertificates]([DocumentId]);

    PRINT 'Created unique index on DocumentId';
END
GO

-- Create index on GeneratedAt
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SigningCertificates]') AND name = 'IX_SigningCertificates_GeneratedAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SigningCertificates_GeneratedAt]
    ON [dbo].[SigningCertificates]([GeneratedAt]);

    PRINT 'Created index on GeneratedAt';
END
GO

-- Create composite index on DocumentHash and CertificateId for verification
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SigningCertificates]') AND name = 'IX_SigningCertificates_Hash_CertId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SigningCertificates_Hash_CertId]
    ON [dbo].[SigningCertificates]([DocumentHash], [CertificateId]);

    PRINT 'Created composite index on DocumentHash and CertificateId';
END
GO

-- Verify the table was created
SELECT
    'SigningCertificates' AS TableName,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'SigningCertificates'
ORDER BY ORDINAL_POSITION;
GO

PRINT 'SigningCertificates migration completed successfully!';
GO
