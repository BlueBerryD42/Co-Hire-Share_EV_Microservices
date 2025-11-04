-- ============================================
-- ADD TEST DATA FOR MEMBER USAGE ANALYSIS
-- ============================================
-- This script adds sample booking data WITHOUT foreign key validation
-- Use this for microservices testing where Users and Groups are in separate databases
--
-- Run this in CoOwnershipVehicle_Booking database

USE CoOwnershipVehicle_Booking;
GO

-- Known IDs from your system:
DECLARE @VehicleId UNIQUEIDENTIFIER = '75134495-27C4-43A9-A549-5624CC91352D'
DECLARE @GroupId UNIQUEIDENTIFIER = NEWID() -- Temporary GUID (Group is in Group Service DB)
DECLARE @Member1Id UNIQUEIDENTIFIER = 'F8A9F5A8-3E4E-4E9A-9A7E-2B8C6F5D8A3B' -- 40% ownership
DECLARE @Member2Id UNIQUEIDENTIFIER = 'E395BDF2-BE3A-4918-AAE0-8CA9F034A6FD' -- 60% ownership
DECLARE @Member3Id UNIQUEIDENTIFIER = '6AE52AB2-FA80-4D9E-A91D-9FED3C703A9D' -- 0% ownership (admin)

PRINT '========================================='
PRINT 'MICROSERVICES TEST DATA SETUP'
PRINT '========================================='
PRINT 'Disabling foreign key constraints...'
PRINT ''

-- Disable BOTH foreign key constraints (Group and User)
ALTER TABLE Bookings NOCHECK CONSTRAINT FK_Bookings_OwnershipGroups_GroupId;
ALTER TABLE Bookings NOCHECK CONSTRAINT FK_Bookings_Users_UserId;
ALTER TABLE Bookings NOCHECK CONSTRAINT FK_Bookings_Vehicles_VehicleId;

PRINT '✅ All FK constraints disabled'
PRINT ''
PRINT '========================================='
PRINT 'ADDING SAMPLE BOOKING DATA'
PRINT '========================================='
PRINT 'Vehicle ID: ' + CAST(@VehicleId AS VARCHAR(50))
PRINT 'Group ID: ' + CAST(@GroupId AS VARCHAR(50))
PRINT 'Member IDs from Auth Service (not in local DB)'
PRINT '========================================='
PRINT ''

-- Member 1: 5 trips (moderate usage - 12 hours total)
-- Trip 1: 2 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member1Id,
        DATEADD(day, -80, GETUTCDATE()), DATEADD(day, -80, DATEADD(hour, 2, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 2: 3 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member1Id,
        DATEADD(day, -60, GETUTCDATE()), DATEADD(day, -60, DATEADD(hour, 3, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 3: 1 hour
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member1Id,
        DATEADD(day, -40, GETUTCDATE()), DATEADD(day, -40, DATEADD(hour, 1, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 4: 4 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member1Id,
        DATEADD(day, -20, GETUTCDATE()), DATEADD(day, -20, DATEADD(hour, 4, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 5: 2 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member1Id,
        DATEADD(day, -5, GETUTCDATE()), DATEADD(day, -5, DATEADD(hour, 2, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

PRINT '✅ Added 5 bookings for Member 1 (40% ownership) - Total: 12 hours'

-- Member 2: 3 trips
-- Trip 6: 2 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member2Id,
        DATEADD(day, -70, GETUTCDATE()), DATEADD(day, -70, DATEADD(hour, 2, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 7: 5 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member2Id,
        DATEADD(day, -45, GETUTCDATE()), DATEADD(day, -45, DATEADD(hour, 5, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 8: 3 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member2Id,
        DATEADD(day, -10, GETUTCDATE()), DATEADD(day, -10, DATEADD(hour, 3, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

PRINT '✅ Added 3 bookings for Member 2 (60% ownership) - Total: 10 hours'

-- Member 3: 2 trips
-- Trip 9: 1 hour
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member3Id,
        DATEADD(day, -55, GETUTCDATE()), DATEADD(day, -55, DATEADD(hour, 1, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

-- Trip 10: 2 hours
INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES (NEWID(), @VehicleId, @GroupId, @Member3Id,
        DATEADD(day, -25, GETUTCDATE()), DATEADD(day, -25, DATEADD(hour, 2, GETUTCDATE())),
        4, 1.0, 0, 1, GETUTCDATE(), GETUTCDATE());

PRINT '✅ Added 2 bookings for Member 3 (0% ownership - admin) - Total: 3 hours'
PRINT ''

PRINT '========================================='
PRINT 'RE-ENABLING FOREIGN KEY CONSTRAINTS'
PRINT '========================================='

-- Re-enable constraints (but don't validate existing data)
ALTER TABLE Bookings WITH NOCHECK CHECK CONSTRAINT FK_Bookings_OwnershipGroups_GroupId;
ALTER TABLE Bookings WITH NOCHECK CHECK CONSTRAINT FK_Bookings_Users_UserId;
ALTER TABLE Bookings WITH NOCHECK CHECK CONSTRAINT FK_Bookings_Vehicles_VehicleId;

PRINT '✅ All FK constraints re-enabled (existing data not validated)'
PRINT ''
PRINT '========================================='
PRINT 'SUMMARY'
PRINT '========================================='
PRINT 'Total Bookings Added: 10 (time-based usage only)'
PRINT ''
PRINT 'Member 1: 5 trips, 12 hours (50% of total trips)'
PRINT 'Member 2: 3 trips, 10 hours (30% of total trips)'
PRINT 'Member 3: 2 trips, 3 hours (20% of total trips)'
PRINT ''
PRINT 'Expected Results in API:'
PRINT '  - Member 1: 50% usage vs 40% ownership = +10% delta (Overutilizing)'
PRINT '  - Member 2: 30% usage vs 60% ownership = -30% delta (Underutilizing)'
PRINT '  - Member 3: 20% usage vs 0% ownership'
PRINT ''
PRINT '⚠️ NOTE: This is MICROSERVICES test data:'
PRINT '  - GroupId: temporary GUID (real groups in Group Service DB)'
PRINT '  - UserIds: from Auth Service DB (not in Booking Service DB)'
PRINT '  - VehicleId: from Vehicle Service DB'
PRINT '  - API will fetch user/group details via HTTP from other services'
PRINT ''

-- Verification Query
PRINT '========================================='
PRINT 'VERIFICATION - Check Added Data'
PRINT '========================================='

SELECT
    b.UserId,
    LEFT(CAST(b.UserId AS VARCHAR(50)), 8) AS UserIdShort,
    COUNT(DISTINCT b.Id) AS TotalTrips,
    SUM(DATEDIFF(HOUR, b.StartAt, b.EndAt)) AS TotalHours,
    MIN(b.StartAt) AS FirstTrip,
    MAX(b.EndAt) AS LastTrip
FROM Bookings b
WHERE b.VehicleId = @VehicleId
  AND b.Status = 4 -- Completed
  AND b.StartAt >= DATEADD(month, -3, GETUTCDATE())
GROUP BY b.UserId
ORDER BY TotalTrips DESC;

PRINT ''
PRINT '✅ TEST DATA READY!'
PRINT ''
PRINT 'Next Steps:'
PRINT '1. Run Postman collection: Vehicle_MemberUsage_API_Tests.postman_collection.json'
PRINT '2. Expected: All 14 requests PASS, 37 test assertions'
PRINT '3. Request 12 will validate: totalUsage ≈ 100% (50% + 30% + 20%)'
PRINT '4. Fairness analysis will show overutilizers and underutilizers'
PRINT '5. Distance fields = 0 (requires CheckIn data from Analytics service)'
PRINT ''

GO

-- ============================================
-- OPTIONAL: Clean up test data
-- ============================================
/*
USE CoOwnershipVehicle_Booking;
GO

DECLARE @VehicleId UNIQUEIDENTIFIER = '75134495-27C4-43A9-A549-5624CC91352D'

-- Disable FK temporarily for cleanup
ALTER TABLE Bookings NOCHECK CONSTRAINT FK_Bookings_OwnershipGroups_GroupId;
ALTER TABLE Bookings NOCHECK CONSTRAINT FK_Bookings_Users_UserId;

-- Delete test bookings
DELETE FROM Bookings
WHERE VehicleId = @VehicleId
  AND StartAt >= DATEADD(month, -3, GETUTCDATE());

-- Re-enable FK
ALTER TABLE Bookings WITH NOCHECK CHECK CONSTRAINT FK_Bookings_OwnershipGroups_GroupId;
ALTER TABLE Bookings WITH NOCHECK CHECK CONSTRAINT FK_Bookings_Users_UserId;

PRINT 'Test data cleaned up'
*/
