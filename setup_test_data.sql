-- =============================================
-- Setup Test Data for Document Paginated API
-- Database: CoOwnershipVehicle_Group
-- =============================================

USE CoOwnershipVehicle_Group;
GO

-- Step 1: Check if the hardcoded test user exists
PRINT '--- Step 1: Checking for test user ---';
DECLARE @TestUserId UNIQUEIDENTIFIER = '196F184E-7D93-4103-BB5A-3C0F78036DD4';

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @TestUserId)
BEGIN
    PRINT 'Creating test user...';
    INSERT INTO Users (Id, Email, FirstName, LastName, Phone, KycStatus, Role, CreatedAt, UpdatedAt)
    VALUES (
        @TestUserId,
        'testuser@coev.com',
        'Test',
        'User',
        '0123456789',
        2, -- Approved
        1, -- User role
        GETUTCDATE(),
        GETUTCDATE()
    );
    PRINT 'Test user created successfully!';
END
ELSE
BEGIN
    PRINT 'Test user already exists.';
END
GO

-- Step 2: Get or create a test group
PRINT '--- Step 2: Setting up test group ---';
DECLARE @TestGroupId UNIQUEIDENTIFIER;
DECLARE @TestUserId UNIQUEIDENTIFIER = '196F184E-7D93-4103-BB5A-3C0F78036DD4';

-- Try to get an existing group
SELECT TOP 1 @TestGroupId = Id FROM OwnershipGroups;

IF @TestGroupId IS NULL
BEGIN
    -- Create a test group if none exists
    SET @TestGroupId = NEWID();
    PRINT 'Creating test group: ' + CAST(@TestGroupId AS VARCHAR(36));

    INSERT INTO OwnershipGroups (Id, Name, Description, Status, CreatedBy, CreatedAt, UpdatedAt)
    VALUES (
        @TestGroupId,
        'Test Group for Document API',
        'This is a test group for testing the document pagination API',
        1, -- Active
        @TestUserId,
        GETUTCDATE(),
        GETUTCDATE()
    );
    PRINT 'Test group created successfully!';
END
ELSE
BEGIN
    PRINT 'Using existing group: ' + CAST(@TestGroupId AS VARCHAR(36));
END

-- Step 3: Ensure test user is a member of the group
PRINT '--- Step 3: Adding user to group ---';
IF NOT EXISTS (SELECT 1 FROM GroupMembers WHERE GroupId = @TestGroupId AND UserId = @TestUserId)
BEGIN
    INSERT INTO GroupMembers (Id, GroupId, UserId, RoleInGroup, SharePercentage, JoinedAt, CreatedAt, UpdatedAt)
    VALUES (
        NEWID(),
        @TestGroupId,
        @TestUserId,
        1, -- Admin
        0.5000, -- 50% share
        GETUTCDATE(),
        GETUTCDATE(),
        GETUTCDATE()
    );
    PRINT 'User added to group as Admin with 50% share.';
END
ELSE
BEGIN
    PRINT 'User is already a member of the group.';
END
GO

-- Step 4: Display the group ID for testing
PRINT '--- Step 4: Group ID for API Testing ---';
DECLARE @TestGroupId UNIQUEIDENTIFIER;
SELECT TOP 1 @TestGroupId = Id FROM OwnershipGroups;

PRINT '======================================';
PRINT 'USE THIS GROUP ID IN YOUR API TESTS:';
PRINT CAST(@TestGroupId AS VARCHAR(36));
PRINT '======================================';
PRINT '';
PRINT 'Example API call:';
PRINT 'GET https://localhost:7071/api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated';
GO

-- Step 5: Check existing documents in the group
PRINT '--- Step 5: Current Documents in Group ---';
DECLARE @TestGroupId UNIQUEIDENTIFIER;
SELECT TOP 1 @TestGroupId = Id FROM OwnershipGroups;

SELECT
    Id,
    FileName,
    Type,
    CASE Type
        WHEN 0 THEN 'OwnershipAgreement'
        WHEN 1 THEN 'MaintenanceContract'
        WHEN 2 THEN 'InsurancePolicy'
        WHEN 3 THEN 'CheckInReport'
        WHEN 4 THEN 'CheckOutReport'
        WHEN 5 THEN 'Other'
    END AS TypeName,
    SignatureStatus,
    CASE SignatureStatus
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'SentForSigning'
        WHEN 2 THEN 'PartiallySigned'
        WHEN 3 THEN 'FullySigned'
        WHEN 4 THEN 'Expired'
        WHEN 5 THEN 'Cancelled'
    END AS StatusName,
    FileSize,
    Description,
    CreatedAt,
    UploadedBy,
    (SELECT COUNT(*) FROM DocumentSignatures WHERE DocumentId = d.Id) as SignatureCount,
    (SELECT COUNT(*) FROM DocumentDownloads WHERE DocumentId = d.Id) as DownloadCount
FROM Documents d
WHERE GroupId = @TestGroupId
ORDER BY CreatedAt DESC;

PRINT '';
PRINT 'Total documents in group: ' + CAST((SELECT COUNT(*) FROM Documents WHERE GroupId = @TestGroupId) AS VARCHAR(10));
GO

-- Step 6: Create sample test documents (only if no documents exist)
PRINT '--- Step 6: Creating Sample Documents (if needed) ---';
DECLARE @TestGroupId UNIQUEIDENTIFIER;
DECLARE @TestUserId UNIQUEIDENTIFIER = '196F184E-7D93-4103-BB5A-3C0F78036DD4';
DECLARE @DocCount INT;

SELECT TOP 1 @TestGroupId = Id FROM OwnershipGroups;
SELECT @DocCount = COUNT(*) FROM Documents WHERE GroupId = @TestGroupId;

IF @DocCount = 0
BEGIN
    PRINT 'No documents found. Creating sample documents for testing...';

    -- Create sample documents
    DECLARE @Doc1 UNIQUEIDENTIFIER = NEWID();
    DECLARE @Doc2 UNIQUEIDENTIFIER = NEWID();
    DECLARE @Doc3 UNIQUEIDENTIFIER = NEWID();
    DECLARE @Doc4 UNIQUEIDENTIFIER = NEWID();
    DECLARE @Doc5 UNIQUEIDENTIFIER = NEWID();

    -- Document 1: Ownership Agreement
    INSERT INTO Documents (
        Id, GroupId, Type, StorageKey, FileName, SignatureStatus,
        Description, FileSize, ContentType, FileHash, IsVirusScanned,
        VirusScanPassed, UploadedBy, CreatedAt, UpdatedAt
    )
    VALUES (
        @Doc1, @TestGroupId, 0, -- OwnershipAgreement
        'documents/' + CAST(@TestGroupId AS VARCHAR(36)) + '/' + CAST(@Doc1 AS VARCHAR(36)) + '.pdf',
        'Main_Ownership_Agreement.pdf', 0, -- Draft
        'Primary ownership agreement for the group',
        1048576, 'application/pdf',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'test1'), 2),
        1, 1, @TestUserId, GETUTCDATE(), GETUTCDATE()
    );

    -- Document 2: Maintenance Contract
    INSERT INTO Documents (
        Id, GroupId, Type, StorageKey, FileName, SignatureStatus,
        Description, FileSize, ContentType, FileHash, IsVirusScanned,
        VirusScanPassed, UploadedBy, CreatedAt, UpdatedAt
    )
    VALUES (
        @Doc2, @TestGroupId, 1, -- MaintenanceContract
        'documents/' + CAST(@TestGroupId AS VARCHAR(36)) + '/' + CAST(@Doc2 AS VARCHAR(36)) + '.pdf',
        'Vehicle_Maintenance_Contract_2025.pdf', 1, -- SentForSigning
        'Annual maintenance contract with service provider',
        2097152, 'application/pdf',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'test2'), 2),
        1, 1, @TestUserId, DATEADD(day, -1, GETUTCDATE()), DATEADD(day, -1, GETUTCDATE())
    );

    -- Document 3: Insurance Policy
    INSERT INTO Documents (
        Id, GroupId, Type, StorageKey, FileName, SignatureStatus,
        Description, FileSize, ContentType, FileHash, IsVirusScanned,
        VirusScanPassed, UploadedBy, CreatedAt, UpdatedAt
    )
    VALUES (
        @Doc3, @TestGroupId, 2, -- InsurancePolicy
        'documents/' + CAST(@TestGroupId AS VARCHAR(36)) + '/' + CAST(@Doc3 AS VARCHAR(36)) + '.pdf',
        'Insurance_Policy_Document.pdf', 3, -- FullySigned
        'Comprehensive insurance coverage for the shared vehicle',
        1536000, 'application/pdf',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'test3'), 2),
        1, 1, @TestUserId, DATEADD(day, -2, GETUTCDATE()), DATEADD(day, -2, GETUTCDATE())
    );

    -- Document 4: Check-In Report (Image)
    INSERT INTO Documents (
        Id, GroupId, Type, StorageKey, FileName, SignatureStatus,
        Description, FileSize, ContentType, FileHash, IsVirusScanned,
        VirusScanPassed, UploadedBy, CreatedAt, UpdatedAt
    )
    VALUES (
        @Doc4, @TestGroupId, 3, -- CheckInReport
        'documents/' + CAST(@TestGroupId AS VARCHAR(36)) + '/' + CAST(@Doc4 AS VARCHAR(36)) + '.jpg',
        'Vehicle_CheckIn_Report_Jan2025.jpg', 0, -- Draft
        'Vehicle condition report from January 2025 check-in',
        512000, 'image/jpeg',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'test4'), 2),
        1, 1, @TestUserId, DATEADD(day, -3, GETUTCDATE()), DATEADD(day, -3, GETUTCDATE())
    );

    -- Document 5: Other
    INSERT INTO Documents (
        Id, GroupId, Type, StorageKey, FileName, SignatureStatus,
        Description, FileSize, ContentType, FileHash, IsVirusScanned,
        VirusScanPassed, UploadedBy, CreatedAt, UpdatedAt
    )
    VALUES (
        @Doc5, @TestGroupId, 5, -- Other
        'documents/' + CAST(@TestGroupId AS VARCHAR(36)) + '/' + CAST(@Doc5 AS VARCHAR(36)) + '.pdf',
        'Meeting_Minutes_October.pdf', 2, -- PartiallySigned
        'Group meeting minutes from October',
        768000, 'application/pdf',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'test5'), 2),
        1, 1, @TestUserId, DATEADD(day, -4, GETUTCDATE()), DATEADD(day, -4, GETUTCDATE())
    );

    PRINT '5 sample documents created successfully!';

    -- Add some signatures to documents
    INSERT INTO DocumentSignatures (Id, DocumentId, SignerId, SignatureOrder, Status, CreatedAt, UpdatedAt)
    VALUES
        (NEWID(), @Doc2, @TestUserId, 1, 0, GETUTCDATE(), GETUTCDATE()),
        (NEWID(), @Doc3, @TestUserId, 1, 3, GETUTCDATE(), GETUTCDATE()),
        (NEWID(), @Doc5, @TestUserId, 1, 2, GETUTCDATE(), GETUTCDATE());

    PRINT 'Sample signatures added!';

    -- Add some download records
    INSERT INTO DocumentDownloads (Id, DocumentId, UserId, IpAddress, UserAgent, DownloadedAt, CreatedAt, UpdatedAt)
    VALUES
        (NEWID(), @Doc2, @TestUserId, '192.168.1.100', 'Mozilla/5.0 (Test)', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),
        (NEWID(), @Doc3, @TestUserId, '192.168.1.100', 'Mozilla/5.0 (Test)', GETUTCDATE(), GETUTCDATE(), GETUTCDATE()),
        (NEWID(), @Doc3, @TestUserId, '192.168.1.100', 'Mozilla/5.0 (Test)', GETUTCDATE(), GETUTCDATE(), GETUTCDATE());

    PRINT 'Sample download records added!';
END
ELSE
BEGIN
    PRINT 'Documents already exist in the group. Skipping sample data creation.';
    PRINT 'Found ' + CAST(@DocCount AS VARCHAR(10)) + ' existing documents.';
END
GO

-- Step 7: Final verification query
PRINT '--- Step 7: Final Verification ---';
DECLARE @TestGroupId UNIQUEIDENTIFIER;
DECLARE @TestUserId UNIQUEIDENTIFIER = '196F184E-7D93-4103-BB5A-3C0F78036DD4';

SELECT TOP 1 @TestGroupId = Id FROM OwnershipGroups;

PRINT '';
PRINT '========================================';
PRINT 'SETUP COMPLETE!';
PRINT '========================================';
PRINT 'Group ID: ' + CAST(@TestGroupId AS VARCHAR(36));
PRINT 'User ID: ' + CAST(@TestUserId AS VARCHAR(36));
PRINT 'Total Documents: ' + CAST((SELECT COUNT(*) FROM Documents WHERE GroupId = @TestGroupId) AS VARCHAR(10));
PRINT '';
PRINT 'API Test Commands:';
PRINT '----------------------------------------';
PRINT '1. Basic pagination:';
PRINT '   GET /api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated';
PRINT '';
PRINT '2. With page size:';
PRINT '   GET /api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated?pageSize=3';
PRINT '';
PRINT '3. Search:';
PRINT '   GET /api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated?searchTerm=contract';
PRINT '';
PRINT '4. Filter by type:';
PRINT '   GET /api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated?documentType=0';
PRINT '';
PRINT '5. Sort by filename:';
PRINT '   GET /api/document/group/' + CAST(@TestGroupId AS VARCHAR(36)) + '/paginated?sortBy=FileName&sortDescending=false';
PRINT '========================================';
GO

-- Display summary table
SELECT
    'Summary' AS Category,
    'Users' AS Item,
    CAST(COUNT(*) AS VARCHAR(10)) AS Count
FROM Users
WHERE Id = '196F184E-7D93-4103-BB5A-3C0F78036DD4'
UNION ALL
SELECT
    'Summary',
    'Groups',
    CAST(COUNT(*) AS VARCHAR(10))
FROM OwnershipGroups
UNION ALL
SELECT
    'Summary',
    'Group Members',
    CAST(COUNT(*) AS VARCHAR(10))
FROM GroupMembers
WHERE UserId = '196F184E-7D93-4103-BB5A-3C0F78036DD4'
UNION ALL
SELECT
    'Summary',
    'Documents',
    CAST(COUNT(*) AS VARCHAR(10))
FROM Documents
WHERE GroupId = (SELECT TOP 1 Id FROM OwnershipGroups)
UNION ALL
SELECT
    'Summary',
    'Signatures',
    CAST(COUNT(*) AS VARCHAR(10))
FROM DocumentSignatures
UNION ALL
SELECT
    'Summary',
    'Downloads',
    CAST(COUNT(*) AS VARCHAR(10))
FROM DocumentDownloads;
GO
