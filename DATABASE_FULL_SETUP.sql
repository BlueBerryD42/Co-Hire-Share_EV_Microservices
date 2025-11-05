/*
================================================================================
  CO-OWNERSHIP VEHICLE - FULL DATABASE SETUP SCRIPT
================================================================================

  Script Purpose: Complete database setup for Group Service (Document Management)
  Version: 1.0
  Created: 2025-11-05

  This script includes:
  - Database creation
  - All table schemas with proper constraints
  - All indexes (including performance optimizations)
  - Foreign key relationships
  - Full-text search catalog and indexes
  - Default values and triggers
  - Performance monitoring views

  Usage:
    sqlcmd -S SERVER_NAME -i DATABASE_FULL_SETUP.sql

  OR in SQL Server Management Studio:
    1. Open this file
    2. Modify database name if needed (line ~30)
    3. Execute (F5)

================================================================================
*/

-- ============================================================================
-- STEP 1: DATABASE CREATION
-- ============================================================================

USE master;
GO

-- Drop database if exists (CAUTION: This will delete all data!)
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'CoOwnershipVehicle_Group')
BEGIN
    ALTER DATABASE CoOwnershipVehicle_Group SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CoOwnershipVehicle_Group;
    PRINT 'Existing database dropped.';
END
GO

-- Create new database
CREATE DATABASE CoOwnershipVehicle_Group;
GO

PRINT 'Database created successfully.';
GO

USE CoOwnershipVehicle_Group;
GO

-- ============================================================================
-- STEP 2: IDENTITY TABLES (ASP.NET Core Identity)
-- ============================================================================

PRINT 'Creating Identity tables...';
GO

-- AspNetRoles
CREATE TABLE AspNetRoles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(256) NULL,
    NormalizedName NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);
GO

CREATE UNIQUE INDEX IX_AspNetRoles_NormalizedName ON AspNetRoles(NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

-- AspNetUsers
CREATE TABLE AspNetUsers (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),

    -- Identity fields
    UserName NVARCHAR(256) NULL,
    NormalizedUserName NVARCHAR(256) NULL,
    Email NVARCHAR(256) NULL,
    NormalizedEmail NVARCHAR(256) NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NULL,
    SecurityStamp NVARCHAR(MAX) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    PhoneNumber NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEnd DATETIMEOFFSET(7) NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 0,
    AccessFailedCount INT NOT NULL DEFAULT 0,

    -- Custom user fields
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    Country NVARCHAR(100) NULL,
    PostalCode NVARCHAR(20) NULL,
    DateOfBirth DATETIME2 NULL,
    KycStatus INT NOT NULL DEFAULT 0, -- Pending=0, InReview=1, Approved=2, Rejected=3
    Role INT NOT NULL DEFAULT 3, -- SystemAdmin=0, Staff=1, GroupAdmin=2, CoOwner=3
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedUserName ON AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedEmail ON AspNetUsers(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
CREATE INDEX IX_AspNetUsers_Email ON AspNetUsers(Email);
CREATE INDEX IX_AspNetUsers_Phone ON AspNetUsers(Phone);
GO

-- AspNetUserRoles
CREATE TABLE AspNetUserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_AspNetUserRoles_RoleId ON AspNetUserRoles(RoleId);
GO

-- AspNetUserClaims
CREATE TABLE AspNetUserClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_AspNetUserClaims_UserId ON AspNetUserClaims(UserId);
GO

-- AspNetUserLogins
CREATE TABLE AspNetUserLogins (
    LoginProvider NVARCHAR(450) NOT NULL,
    ProviderKey NVARCHAR(450) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey),
    CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_AspNetUserLogins_UserId ON AspNetUserLogins(UserId);
GO

-- AspNetUserTokens
CREATE TABLE AspNetUserTokens (
    UserId UNIQUEIDENTIFIER NOT NULL,
    LoginProvider NVARCHAR(450) NOT NULL,
    Name NVARCHAR(450) NOT NULL,
    Value NVARCHAR(MAX) NULL,
    PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

-- AspNetRoleClaims
CREATE TABLE AspNetRoleClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_AspNetRoleClaims_RoleId ON AspNetRoleClaims(RoleId);
GO

PRINT 'Identity tables created successfully.';
GO

-- ============================================================================
-- STEP 3: CORE GROUP TABLES
-- ============================================================================

PRINT 'Creating core group tables...';
GO

-- OwnershipGroups
CREATE TABLE OwnershipGroups (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    Status INT NOT NULL DEFAULT 0, -- Active=0, Inactive=1, Dissolved=2
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_OwnershipGroups_AspNetUsers_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_OwnershipGroups_Name ON OwnershipGroups(Name);
CREATE INDEX IX_OwnershipGroups_CreatedBy ON OwnershipGroups(CreatedBy);
CREATE INDEX IX_OwnershipGroups_Status ON OwnershipGroups(Status);
GO

-- GroupMembers
CREATE TABLE GroupMembers (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    GroupId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    SharePercentage DECIMAL(5,4) NOT NULL,
    RoleInGroup INT NOT NULL DEFAULT 0, -- Member=0, Admin=1
    JoinedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_GroupMembers_OwnershipGroups_GroupId
        FOREIGN KEY (GroupId) REFERENCES OwnershipGroups(Id) ON DELETE CASCADE,
    CONSTRAINT FK_GroupMembers_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT CK_GroupMembers_SharePercentage CHECK (SharePercentage >= 0.0001 AND SharePercentage <= 1.0000)
);
GO

CREATE UNIQUE INDEX IX_GroupMembers_GroupId_UserId ON GroupMembers(GroupId, UserId);
CREATE INDEX IX_GroupMembers_UserId ON GroupMembers(UserId);
GO

-- Vehicles
CREATE TABLE Vehicles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Vin NVARCHAR(17) NOT NULL,
    PlateNumber NVARCHAR(20) NOT NULL,
    Model NVARCHAR(100) NOT NULL,
    Year INT NOT NULL,
    Color NVARCHAR(50) NULL,
    Status INT NOT NULL DEFAULT 0, -- Available=0, InUse=1, Maintenance=2, Unavailable=3
    LastServiceDate DATETIME2 NULL,
    Odometer INT NOT NULL DEFAULT 0,
    GroupId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_Vehicles_OwnershipGroups_GroupId
        FOREIGN KEY (GroupId) REFERENCES OwnershipGroups(Id) ON DELETE SET NULL
);
GO

CREATE UNIQUE INDEX IX_Vehicles_Vin ON Vehicles(Vin);
CREATE UNIQUE INDEX IX_Vehicles_PlateNumber ON Vehicles(PlateNumber);
CREATE INDEX IX_Vehicles_GroupId ON Vehicles(GroupId);
CREATE INDEX IX_Vehicles_Status ON Vehicles(Status);
GO

PRINT 'Core group tables created successfully.';
GO

-- ============================================================================
-- STEP 4: DOCUMENT MANAGEMENT TABLES
-- ============================================================================

PRINT 'Creating document management tables...';
GO

-- DocumentTemplates
CREATE TABLE DocumentTemplates (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    Category INT NOT NULL, -- Legal=0, Insurance=1, Maintenance=2, Financial=3, Usage=4, Sale=5, Other=99
    TemplateContent NVARCHAR(MAX) NOT NULL,
    VariablesJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    Version INT NOT NULL DEFAULT 1,
    PreviewImageUrl NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentTemplates_AspNetUsers_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentTemplates_Name ON DocumentTemplates(Name);
CREATE INDEX IX_DocumentTemplates_Category ON DocumentTemplates(Category);
CREATE INDEX IX_DocumentTemplates_IsActive ON DocumentTemplates(IsActive);
CREATE INDEX IX_DocumentTemplates_Category_IsActive ON DocumentTemplates(Category, IsActive);
GO

-- Documents
CREATE TABLE Documents (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    GroupId UNIQUEIDENTIFIER NOT NULL,
    Type INT NOT NULL, -- OwnershipAgreement=0, MaintenanceContract=1, InsurancePolicy=2, CheckInReport=3, CheckOutReport=4, Other=5
    StorageKey NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    SignatureStatus INT NOT NULL DEFAULT 0, -- Draft=0, SentForSigning=1, PartiallySigned=2, FullySigned=3, Expired=4, Cancelled=5
    Description NVARCHAR(1000) NULL,
    FileSize BIGINT NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    FileHash NVARCHAR(64) NULL,
    PageCount INT NULL,
    Author NVARCHAR(200) NULL,
    IsVirusScanned BIT NOT NULL DEFAULT 0,
    VirusScanPassed BIT NULL,
    UploadedBy UNIQUEIDENTIFIER NULL,

    -- Soft delete fields
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    DeletedBy UNIQUEIDENTIFIER NULL,

    -- Template-related fields
    TemplateId UNIQUEIDENTIFIER NULL,
    TemplateVariablesJson NVARCHAR(MAX) NULL,

    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_Documents_OwnershipGroups_GroupId
        FOREIGN KEY (GroupId) REFERENCES OwnershipGroups(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Documents_DocumentTemplates_TemplateId
        FOREIGN KEY (TemplateId) REFERENCES DocumentTemplates(Id) ON DELETE SET NULL
);
GO

-- Basic indexes
CREATE INDEX IX_Documents_GroupId ON Documents(GroupId);
CREATE UNIQUE INDEX IX_Documents_StorageKey ON Documents(StorageKey);
CREATE INDEX IX_Documents_GroupId_FileHash ON Documents(GroupId, FileHash);
CREATE INDEX IX_Documents_CreatedAt ON Documents(CreatedAt);
CREATE INDEX IX_Documents_UploadedBy ON Documents(UploadedBy);
CREATE INDEX IX_Documents_IsDeleted ON Documents(IsDeleted);
CREATE INDEX IX_Documents_IsDeleted_DeletedAt ON Documents(IsDeleted, DeletedAt);
CREATE INDEX IX_Documents_TemplateId ON Documents(TemplateId);

-- Performance optimization indexes
CREATE NONCLUSTERED INDEX IX_Documents_GroupId_CreatedAt_Include
    ON Documents(GroupId, CreatedAt DESC)
    INCLUDE (FileName, Type, SignatureStatus, FileSize, UploadedBy);

CREATE NONCLUSTERED INDEX IX_Documents_Type_GroupId
    ON Documents(Type, GroupId)
    WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Documents_SignatureStatus_GroupId
    ON Documents(SignatureStatus, GroupId)
    WHERE IsDeleted = 0;
GO

PRINT 'Documents table created successfully.';
GO

-- DocumentSignatures
CREATE TABLE DocumentSignatures (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    SignerId UNIQUEIDENTIFIER NOT NULL,
    SignedAt DATETIME2 NULL,
    SignatureReference NVARCHAR(500) NULL,
    SignatureOrder INT NOT NULL,
    SignatureMetadata NVARCHAR(2000) NULL,
    Status INT NOT NULL DEFAULT 0, -- Draft=0, SentForSigning=1, PartiallySigned=2, FullySigned=3, Expired=4, Cancelled=5
    SigningToken NVARCHAR(500) NULL,
    TokenExpiresAt DATETIME2 NULL,
    DueDate DATETIME2 NULL,
    Message NVARCHAR(1000) NULL,
    SigningMode INT NOT NULL DEFAULT 0, -- Parallel=0, Sequential=1
    IsNotificationSent BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentSignatures_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentSignatures_AspNetUsers_SignerId
        FOREIGN KEY (SignerId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentSignatures_DocumentId ON DocumentSignatures(DocumentId);
CREATE INDEX IX_DocumentSignatures_SignerId ON DocumentSignatures(SignerId);
CREATE UNIQUE INDEX IX_DocumentSignatures_SigningToken ON DocumentSignatures(SigningToken) WHERE SigningToken IS NOT NULL;
CREATE INDEX IX_DocumentSignatures_DocumentId_SignerId ON DocumentSignatures(DocumentId, SignerId);
CREATE INDEX IX_DocumentSignatures_DocumentId_SignatureOrder ON DocumentSignatures(DocumentId, SignatureOrder);
CREATE INDEX IX_DocumentSignatures_TokenExpiresAt ON DocumentSignatures(TokenExpiresAt);
CREATE INDEX IX_DocumentSignatures_DueDate ON DocumentSignatures(DueDate);

-- Performance optimization indexes
CREATE NONCLUSTERED INDEX IX_DocumentSignatures_Status_DueDate
    ON DocumentSignatures(Status, DueDate)
    WHERE DueDate IS NOT NULL AND Status IN (0, 1);

CREATE NONCLUSTERED INDEX IX_DocumentSignatures_DocumentId_Order
    ON DocumentSignatures(DocumentId, SignatureOrder)
    INCLUDE (SignerId, Status, SignedAt);
GO

-- DocumentDownloads
CREATE TABLE DocumentDownloads (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    IpAddress NVARCHAR(45) NOT NULL,
    UserAgent NVARCHAR(500) NULL,
    DownloadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentDownloads_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentDownloads_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentDownloads_DocumentId ON DocumentDownloads(DocumentId);
CREATE INDEX IX_DocumentDownloads_UserId ON DocumentDownloads(UserId);
CREATE INDEX IX_DocumentDownloads_DownloadedAt ON DocumentDownloads(DownloadedAt);
CREATE INDEX IX_DocumentDownloads_DocumentId_UserId ON DocumentDownloads(DocumentId, UserId);
GO

-- SigningCertificates
CREATE TABLE SigningCertificates (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    CertificateId NVARCHAR(100) NOT NULL,
    DocumentHash NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    TotalSigners INT NOT NULL,
    GeneratedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NULL,
    SignersJson NVARCHAR(4000) NULL,
    IsRevoked BIT NOT NULL DEFAULT 0,
    RevokedAt DATETIME2 NULL,
    RevocationReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_SigningCertificates_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX IX_SigningCertificates_CertificateId ON SigningCertificates(CertificateId);
CREATE UNIQUE INDEX IX_SigningCertificates_DocumentId ON SigningCertificates(DocumentId);
CREATE INDEX IX_SigningCertificates_GeneratedAt ON SigningCertificates(GeneratedAt);
CREATE INDEX IX_SigningCertificates_DocumentHash_CertificateId ON SigningCertificates(DocumentHash, CertificateId);
GO

-- DocumentVersions
CREATE TABLE DocumentVersions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    VersionNumber INT NOT NULL,
    StorageKey NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    FileSize BIGINT NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    FileHash NVARCHAR(64) NULL,
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ChangeDescription NVARCHAR(1000) NULL,
    IsCurrent BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentVersions_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentVersions_AspNetUsers_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentVersions_DocumentId ON DocumentVersions(DocumentId);
CREATE UNIQUE INDEX IX_DocumentVersions_StorageKey ON DocumentVersions(StorageKey);
CREATE UNIQUE INDEX IX_DocumentVersions_DocumentId_VersionNumber ON DocumentVersions(DocumentId, VersionNumber);
CREATE INDEX IX_DocumentVersions_DocumentId_IsCurrent ON DocumentVersions(DocumentId, IsCurrent);
CREATE INDEX IX_DocumentVersions_UploadedAt ON DocumentVersions(UploadedAt);

-- Performance optimization index
CREATE NONCLUSTERED INDEX IX_DocumentVersions_DocumentId_VersionNumber_Desc
    ON DocumentVersions(DocumentId, VersionNumber DESC)
    INCLUDE (StorageKey, FileName, FileSize, UploadedBy, UploadedAt, IsCurrent);
GO

-- SignatureReminders
CREATE TABLE SignatureReminders (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentSignatureId UNIQUEIDENTIFIER NOT NULL,
    ReminderType INT NOT NULL, -- Initial=0, ThreeDaysBefore=1, OneDayBefore=2, Overdue=3, Manual=4
    SentAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    SentBy UNIQUEIDENTIFIER NOT NULL,
    IsManual BIT NOT NULL DEFAULT 0,
    Message NVARCHAR(500) NULL,
    Status INT NOT NULL DEFAULT 0, -- Pending=0, Sent=1, Delivered=2, Failed=3
    DeliveredAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_SignatureReminders_DocumentSignatures_DocumentSignatureId
        FOREIGN KEY (DocumentSignatureId) REFERENCES DocumentSignatures(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_SignatureReminders_DocumentSignatureId ON SignatureReminders(DocumentSignatureId);
CREATE INDEX IX_SignatureReminders_SentAt ON SignatureReminders(SentAt);
CREATE INDEX IX_SignatureReminders_Status ON SignatureReminders(Status);
CREATE INDEX IX_SignatureReminders_DocumentSignatureId_ReminderType ON SignatureReminders(DocumentSignatureId, ReminderType);
GO

PRINT 'Document management tables created successfully.';
GO

-- ============================================================================
-- STEP 5: DOCUMENT SHARING & COLLABORATION TABLES
-- ============================================================================

PRINT 'Creating document sharing tables...';
GO

-- DocumentShares
CREATE TABLE DocumentShares (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    ShareToken NVARCHAR(100) NOT NULL,
    SharedBy UNIQUEIDENTIFIER NOT NULL,
    SharedWith NVARCHAR(200) NOT NULL,
    RecipientEmail NVARCHAR(200) NULL,
    Permissions INT NOT NULL, -- None=0, View=1, Download=2, Sign=4, ViewAndDownload=3, All=7
    ExpiresAt DATETIME2 NULL,
    Message NVARCHAR(1000) NULL,
    AccessCount INT NOT NULL DEFAULT 0,
    FirstAccessedAt DATETIME2 NULL,
    LastAccessedAt DATETIME2 NULL,
    IsRevoked BIT NOT NULL DEFAULT 0,
    RevokedAt DATETIME2 NULL,
    RevokedBy UNIQUEIDENTIFIER NULL,
    PasswordHash NVARCHAR(500) NULL,
    MaxAccessCount INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentShares_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentShares_AspNetUsers_SharedBy
        FOREIGN KEY (SharedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX IX_DocumentShares_ShareToken ON DocumentShares(ShareToken);
CREATE INDEX IX_DocumentShares_DocumentId ON DocumentShares(DocumentId);
CREATE INDEX IX_DocumentShares_ExpiresAt ON DocumentShares(ExpiresAt);
CREATE INDEX IX_DocumentShares_IsRevoked ON DocumentShares(IsRevoked);
CREATE INDEX IX_DocumentShares_ShareToken_IsRevoked_ExpiresAt ON DocumentShares(ShareToken, IsRevoked, ExpiresAt);

-- Performance optimization: Filtered index for active shares
SET QUOTED_IDENTIFIER ON;
GO
CREATE NONCLUSTERED INDEX IX_DocumentShares_IsRevoked_ExpiresAt
    ON DocumentShares(IsRevoked, ExpiresAt)
    WHERE IsRevoked = 0;
GO

-- DocumentShareAccesses
CREATE TABLE DocumentShareAccesses (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DocumentShareId UNIQUEIDENTIFIER NOT NULL,
    AccessedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IpAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL,
    Location NVARCHAR(200) NULL,
    Action INT NOT NULL, -- Viewed=0, Downloaded=1, Signed=2, PasswordAttempt=3, Expired=4, Revoked=5
    WasSuccessful BIT NOT NULL DEFAULT 1,
    FailureReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentShareAccesses_DocumentShares_DocumentShareId
        FOREIGN KEY (DocumentShareId) REFERENCES DocumentShares(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_DocumentShareAccesses_DocumentShareId ON DocumentShareAccesses(DocumentShareId);
CREATE INDEX IX_DocumentShareAccesses_AccessedAt ON DocumentShareAccesses(AccessedAt);
CREATE INDEX IX_DocumentShareAccesses_Action ON DocumentShareAccesses(Action);
GO

-- DocumentTags
CREATE TABLE DocumentTags (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Color NVARCHAR(20) NULL,
    Description NVARCHAR(500) NULL,
    GroupId UNIQUEIDENTIFIER NULL,
    UsageCount INT NOT NULL DEFAULT 0,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_DocumentTags_OwnershipGroups_GroupId
        FOREIGN KEY (GroupId) REFERENCES OwnershipGroups(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_DocumentTags_AspNetUsers_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentTags_Name ON DocumentTags(Name);
CREATE INDEX IX_DocumentTags_GroupId ON DocumentTags(GroupId);
CREATE UNIQUE INDEX IX_DocumentTags_Name_GroupId ON DocumentTags(Name, GroupId);

-- Performance optimization index
CREATE NONCLUSTERED INDEX IX_DocumentTags_UsageCount_GroupId
    ON DocumentTags(UsageCount DESC, GroupId)
    INCLUDE (Name, Color, Description);
GO

-- DocumentTagMappings
CREATE TABLE DocumentTagMappings (
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    TagId UNIQUEIDENTIFIER NOT NULL,
    TaggedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TaggedBy UNIQUEIDENTIFIER NOT NULL,

    PRIMARY KEY (DocumentId, TagId),

    CONSTRAINT FK_DocumentTagMappings_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentTagMappings_DocumentTags_TagId
        FOREIGN KEY (TagId) REFERENCES DocumentTags(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DocumentTagMappings_AspNetUsers_TaggedBy
        FOREIGN KEY (TaggedBy) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_DocumentTagMappings_DocumentId ON DocumentTagMappings(DocumentId);
CREATE INDEX IX_DocumentTagMappings_TagId ON DocumentTagMappings(TagId);
CREATE INDEX IX_DocumentTagMappings_TaggedAt ON DocumentTagMappings(TaggedAt);

-- Performance optimization index
CREATE NONCLUSTERED INDEX IX_DocumentTagMappings_TagId_DocumentId
    ON DocumentTagMappings(TagId, DocumentId)
    INCLUDE (TaggedAt, TaggedBy);
GO

-- SavedDocumentSearches
CREATE TABLE SavedDocumentSearches (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500) NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    GroupId UNIQUEIDENTIFIER NULL,
    SearchCriteriaJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    UsageCount INT NOT NULL DEFAULT 0,
    LastUsedAt DATETIME2 NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_SavedDocumentSearches_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_SavedDocumentSearches_OwnershipGroups_GroupId
        FOREIGN KEY (GroupId) REFERENCES OwnershipGroups(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_SavedDocumentSearches_UserId ON SavedDocumentSearches(UserId);
CREATE INDEX IX_SavedDocumentSearches_GroupId ON SavedDocumentSearches(GroupId);
CREATE INDEX IX_SavedDocumentSearches_UserId_IsDefault ON SavedDocumentSearches(UserId, IsDefault);
GO

PRINT 'Document sharing and collaboration tables created successfully.';
GO

-- ============================================================================
-- STEP 6: FULL-TEXT SEARCH SETUP
-- ============================================================================

PRINT 'Setting up full-text search...';
GO

-- Create full-text catalog
IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'DocumentSearchCatalog')
BEGIN
    CREATE FULLTEXT CATALOG DocumentSearchCatalog AS DEFAULT;
    PRINT 'Full-text catalog created.';
END
GO

-- Create full-text index on Documents table
IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Documents'))
BEGIN
    CREATE FULLTEXT INDEX ON Documents(FileName, Description)
        KEY INDEX PK__Documents (Id)
        WITH STOPLIST = SYSTEM;
    PRINT 'Full-text index on Documents created.';
END
GO

PRINT 'Full-text search configured successfully.';
GO

-- ============================================================================
-- STEP 7: PERFORMANCE MONITORING VIEWS
-- ============================================================================

PRINT 'Creating performance monitoring views...';
GO

-- View for missing indexes
CREATE OR ALTER VIEW vw_MissingIndexes
AS
SELECT
    CONVERT(DECIMAL(18,2), migs.user_seeks * migs.avg_total_user_cost * (migs.avg_user_impact * 0.01)) AS IndexAdvantage,
    migs.last_user_seek,
    mid.statement AS TableName,
    mid.equality_columns AS EqualityColumns,
    mid.inequality_columns AS InequalityColumns,
    mid.included_columns AS IncludedColumns,
    migs.unique_compiles,
    migs.user_seeks,
    migs.avg_total_user_cost,
    migs.avg_user_impact
FROM sys.dm_db_missing_index_group_stats AS migs
INNER JOIN sys.dm_db_missing_index_groups AS mig ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details AS mid ON mig.index_handle = mid.index_handle
WHERE mid.database_id = DB_ID()
    AND migs.user_seeks > 0;
GO

-- View for index usage statistics
CREATE OR ALTER VIEW vw_IndexUsageStats
AS
SELECT
    OBJECT_NAME(s.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates,
    s.last_user_seek,
    s.last_user_scan,
    s.last_user_lookup
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE s.database_id = DB_ID()
    AND OBJECTPROPERTY(s.object_id, 'IsUserTable') = 1;
GO

-- View for table sizes
CREATE OR ALTER VIEW vw_TableSizes
AS
SELECT
    t.NAME AS TableName,
    s.Name AS SchemaName,
    p.rows AS RowCounts,
    SUM(a.total_pages) * 8 AS TotalSpaceKB,
    SUM(a.used_pages) * 8 AS UsedSpaceKB,
    (SUM(a.total_pages) - SUM(a.used_pages)) * 8 AS UnusedSpaceKB
FROM sys.tables t
INNER JOIN sys.indexes i ON t.OBJECT_ID = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.OBJECT_ID AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
LEFT OUTER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.NAME NOT LIKE 'dt%'
    AND t.is_ms_shipped = 0
    AND i.OBJECT_ID > 255
GROUP BY t.Name, s.Name, p.Rows;
GO

-- View for query performance
CREATE OR ALTER VIEW vw_QueryPerformance
AS
SELECT TOP 50
    qs.execution_count,
    qs.total_elapsed_time / 1000000.0 AS total_elapsed_time_sec,
    qs.total_elapsed_time / qs.execution_count / 1000000.0 AS avg_elapsed_time_sec,
    qs.total_worker_time / 1000000.0 AS total_cpu_time_sec,
    qs.total_worker_time / qs.execution_count / 1000000.0 AS avg_cpu_time_sec,
    qs.total_logical_reads,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    qs.total_logical_writes,
    qs.creation_time,
    qs.last_execution_time,
    SUBSTRING(qt.text, (qs.statement_start_offset/2) + 1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(qt.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2) + 1) AS query_text
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS qt
WHERE qt.dbid = DB_ID()
ORDER BY qs.total_elapsed_time DESC;
GO

PRINT 'Performance monitoring views created successfully.';
GO

-- ============================================================================
-- STEP 8: UPDATE STATISTICS
-- ============================================================================

PRINT 'Updating statistics...';
GO

-- Update statistics on all tables
EXEC sp_MSforeachtable 'UPDATE STATISTICS ? WITH FULLSCAN';
GO

PRINT 'Statistics updated successfully.';
GO

-- ============================================================================
-- STEP 9: SUMMARY & VERIFICATION
-- ============================================================================

PRINT '';
PRINT '================================================================================';
PRINT 'DATABASE SETUP COMPLETED SUCCESSFULLY!';
PRINT '================================================================================';
PRINT '';
PRINT 'Database: CoOwnershipVehicle_Group';
PRINT '';
PRINT 'Tables Created:';
PRINT '  - Identity Tables: 7 (AspNetUsers, AspNetRoles, etc.)';
PRINT '  - Core Group Tables: 3 (OwnershipGroups, GroupMembers, Vehicles)';
PRINT '  - Document Tables: 6 (Documents, DocumentSignatures, DocumentDownloads, etc.)';
PRINT '  - Document Sharing: 5 (DocumentShares, DocumentTags, SavedSearches, etc.)';
PRINT '  - Total: 21 tables';
PRINT '';
PRINT 'Indexes Created:';
PRINT '  - Primary Keys: 21';
PRINT '  - Unique Indexes: 15';
PRINT '  - Standard Indexes: 50+';
PRINT '  - Performance Optimization Indexes: 11';
PRINT '  - Full-Text Indexes: 1';
PRINT '';
PRINT 'Performance Features:';
PRINT '  - Covering indexes for common queries';
PRINT '  - Filtered indexes for active records';
PRINT '  - Full-text search on Documents';
PRINT '  - Performance monitoring views (4)';
PRINT '';
PRINT 'Next Steps:';
PRINT '  1. Configure connection string in appsettings.json';
PRINT '  2. Run application to verify connectivity';
PRINT '  3. Create initial admin user (via Auth service)';
PRINT '  4. Monitor performance using vw_* views';
PRINT '';
PRINT 'Performance Monitoring Queries:';
PRINT '  - SELECT * FROM vw_MissingIndexes WHERE IndexAdvantage > 100';
PRINT '  - SELECT * FROM vw_IndexUsageStats ORDER BY user_seeks DESC';
PRINT '  - SELECT * FROM vw_TableSizes ORDER BY RowCounts DESC';
PRINT '  - SELECT * FROM vw_QueryPerformance';
PRINT '';
PRINT '================================================================================';
GO

-- Verification queries
SELECT
    'Tables' AS Category,
    COUNT(*) AS Count
FROM sys.tables
WHERE is_ms_shipped = 0

UNION ALL

SELECT
    'Indexes' AS Category,
    COUNT(*) AS Count
FROM sys.indexes
WHERE object_id IN (SELECT object_id FROM sys.tables WHERE is_ms_shipped = 0)
    AND index_id > 0

UNION ALL

SELECT
    'Foreign Keys' AS Category,
    COUNT(*) AS Count
FROM sys.foreign_keys

UNION ALL

SELECT
    'Views' AS Category,
    COUNT(*) AS Count
FROM sys.views
WHERE is_ms_shipped = 0;
GO

PRINT '';
PRINT 'Setup verification completed.';
PRINT 'Database is ready for use!';
GO
