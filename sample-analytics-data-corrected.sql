-- Co-Ownership Vehicle Analytics - Corrected Sample Data Script
-- This script creates sample data for testing the Analytics API endpoints
-- Run this script against the CoOwnershipVehicle_Analytics database

USE [CoOwnershipVehicle_Analytics]
GO

-- Clear existing data to avoid conflicts
PRINT 'Clearing existing data...'
DELETE FROM AnalyticsSnapshots
DELETE FROM UserAnalytics
DELETE FROM VehicleAnalytics  
DELETE FROM GroupAnalytics
DELETE FROM Bookings
DELETE FROM CheckIns
DELETE FROM Payments
DELETE FROM Invoices
DELETE FROM LedgerEntries
DELETE FROM Notifications
DELETE FROM OwnershipGroups
DELETE FROM Proposals
DELETE FROM Users
DELETE FROM Vehicles
DELETE FROM Votes
DELETE FROM GroupMembers
GO

PRINT 'Creating sample data for Analytics service testing...'
PRINT '======================================================='

-- 1. Create Users first (required for foreign keys)
PRINT 'Creating Users...'

DECLARE @User1 UNIQUEIDENTIFIER = NEWID()
DECLARE @User2 UNIQUEIDENTIFIER = NEWID()
DECLARE @User3 UNIQUEIDENTIFIER = NEWID()
DECLARE @User4 UNIQUEIDENTIFIER = NEWID()
DECLARE @User5 UNIQUEIDENTIFIER = NEWID()
DECLARE @User6 UNIQUEIDENTIFIER = NEWID()
DECLARE @User7 UNIQUEIDENTIFIER = NEWID()
DECLARE @User8 UNIQUEIDENTIFIER = NEWID()
DECLARE @User9 UNIQUEIDENTIFIER = NEWID()
DECLARE @User10 UNIQUEIDENTIFIER = NEWID()

INSERT INTO Users (Id, FirstName, LastName, Phone, Address, City, Country, PostalCode, DateOfBirth, KycStatus, Role, CreatedAt, UpdatedAt, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount)
VALUES 
    (@User1, 'John', 'Smith', '+1234567890', '123 Main St', 'New York', 'USA', '10001', '1990-01-15', 2, 3, GETDATE(), GETDATE(), 'john.smith', 'JOHN.SMITH', 'john.smith@example.com', 'JOHN.SMITH@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp1', 'ConcurrencyStamp1', '+1234567890', 1, 0, NULL, 1, 0),
    (@User2, 'Sarah', 'Johnson', '+1234567891', '456 Oak Ave', 'Los Angeles', 'USA', '90210', '1985-03-22', 2, 3, GETDATE(), GETDATE(), 'sarah.johnson', 'SARAH.JOHNSON', 'sarah.johnson@example.com', 'SARAH.JOHNSON@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp2', 'ConcurrencyStamp2', '+1234567891', 1, 0, NULL, 1, 0),
    (@User3, 'Mike', 'Wilson', '+1234567892', '789 Pine St', 'Chicago', 'USA', '60601', '1988-07-10', 2, 3, GETDATE(), GETDATE(), 'mike.wilson', 'MIKE.WILSON', 'mike.wilson@example.com', 'MIKE.WILSON@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp3', 'ConcurrencyStamp3', '+1234567892', 1, 0, NULL, 1, 0),
    (@User4, 'Emily', 'Davis', '+1234567893', '321 Elm St', 'Houston', 'USA', '77001', '1992-11-05', 2, 3, GETDATE(), GETDATE(), 'emily.davis', 'EMILY.DAVIS', 'emily.davis@example.com', 'EMILY.DAVIS@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp4', 'ConcurrencyStamp4', '+1234567893', 1, 0, NULL, 1, 0),
    (@User5, 'David', 'Brown', '+1234567894', '654 Maple Dr', 'Phoenix', 'USA', '85001', '1987-09-18', 2, 3, GETDATE(), GETDATE(), 'david.brown', 'DAVID.BROWN', 'david.brown@example.com', 'DAVID.BROWN@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp5', 'ConcurrencyStamp5', '+1234567894', 1, 0, NULL, 1, 0),
    (@User6, 'Lisa', 'Anderson', '+1234567895', '987 Cedar Ln', 'Philadelphia', 'USA', '19101', '1991-04-12', 2, 3, GETDATE(), GETDATE(), 'lisa.anderson', 'LISA.ANDERSON', 'lisa.anderson@example.com', 'LISA.ANDERSON@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp6', 'ConcurrencyStamp6', '+1234567895', 1, 0, NULL, 1, 0),
    (@User7, 'Tom', 'Miller', '+1234567896', '147 Birch St', 'San Antonio', 'USA', '78201', '1989-12-03', 2, 3, GETDATE(), GETDATE(), 'tom.miller', 'TOM.MILLER', 'tom.miller@example.com', 'TOM.MILLER@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp7', 'ConcurrencyStamp7', '+1234567896', 1, 0, NULL, 1, 0),
    (@User8, 'Anna', 'Garcia', '+1234567897', '258 Spruce Ave', 'San Diego', 'USA', '92101', '1993-06-25', 2, 3, GETDATE(), GETDATE(), 'anna.garcia', 'ANNA.GARCIA', 'anna.garcia@example.com', 'ANNA.GARCIA@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp8', 'ConcurrencyStamp8', '+1234567897', 1, 0, NULL, 1, 0),
    (@User9, 'Chris', 'Taylor', '+1234567898', '369 Willow Way', 'Dallas', 'USA', '75201', '1986-08-14', 2, 3, GETDATE(), GETDATE(), 'chris.taylor', 'CHRIS.TAYLOR', 'chris.taylor@example.com', 'CHRIS.TAYLOR@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp9', 'ConcurrencyStamp9', '+1234567898', 1, 0, NULL, 1, 0),
    (@User10, 'Maria', 'Rodriguez', '+1234567899', '741 Poplar Pl', 'San Jose', 'USA', '95101', '1994-02-28', 2, 3, GETDATE(), GETDATE(), 'maria.rodriguez', 'MARIA.RODRIGUEZ', 'maria.rodriguez@example.com', 'MARIA.RODRIGUEZ@EXAMPLE.COM', 1, 'AQAAAAEAACcQAAAAEHash', 'SecurityStamp10', 'ConcurrencyStamp10', '+1234567899', 1, 0, NULL, 1, 0)

-- 2. Create Ownership Groups (after users are created)
PRINT 'Creating Ownership Groups...'

DECLARE @Group1 UNIQUEIDENTIFIER = NEWID()
DECLARE @Group2 UNIQUEIDENTIFIER = NEWID()
DECLARE @Group3 UNIQUEIDENTIFIER = NEWID()
DECLARE @Group4 UNIQUEIDENTIFIER = NEWID()
DECLARE @Group5 UNIQUEIDENTIFIER = NEWID()

INSERT INTO OwnershipGroups (Id, Name, Description, Status, CreatedBy, CreatedAt, UpdatedAt)
VALUES 
    (@Group1, 'Downtown EV Group', 'Premium electric vehicle sharing group in downtown area', 1, @User1, GETDATE(), GETDATE()),
    (@Group2, 'Tech Campus Commuters', 'Electric vehicle sharing for tech campus employees', 1, @User2, GETDATE(), GETDATE()),
    (@Group3, 'Green City Riders', 'Eco-friendly vehicle sharing community', 1, @User3, GETDATE(), GETDATE()),
    (@Group4, 'University Students', 'Student-focused electric vehicle sharing', 1, @User4, GETDATE(), GETDATE()),
    (@Group5, 'Corporate Fleet', 'Corporate electric vehicle sharing program', 1, @User5, GETDATE(), GETDATE())

-- 3. Create Vehicles
PRINT 'Creating Vehicles...'

DECLARE @Vehicle1 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle2 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle3 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle4 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle5 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle6 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle7 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle8 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle9 UNIQUEIDENTIFIER = NEWID()
DECLARE @Vehicle10 UNIQUEIDENTIFIER = NEWID()

INSERT INTO Vehicles (Id, Vin, PlateNumber, Model, Year, Color, Status, LastServiceDate, Odometer, GroupId, CreatedAt, UpdatedAt)
VALUES 
    (@Vehicle1, '1HGBH41JXMN109186', 'EV-001', 'Tesla Model 3', 2023, 'White', 1, '2024-04-15', 15000, @Group1, GETDATE(), GETDATE()),
    (@Vehicle2, '1HGBH41JXMN109187', 'EV-002', 'BMW i3', 2023, 'Blue', 1, '2024-04-10', 12000, @Group1, GETDATE(), GETDATE()),
    (@Vehicle3, '1HGBH41JXMN109188', 'EV-003', 'Nissan Leaf', 2023, 'Red', 1, '2024-04-20', 18000, @Group2, GETDATE(), GETDATE()),
    (@Vehicle4, '1HGBH41JXMN109189', 'EV-004', 'Audi e-tron', 2023, 'Black', 1, '2024-04-05', 14000, @Group2, GETDATE(), GETDATE()),
    (@Vehicle5, '1HGBH41JXMN109190', 'EV-005', 'Hyundai Kona Electric', 2023, 'Silver', 1, '2024-04-12', 16000, @Group3, GETDATE(), GETDATE()),
    (@Vehicle6, '1HGBH41JXMN109191', 'EV-006', 'Chevrolet Bolt', 2023, 'Green', 1, '2024-04-18', 13000, @Group3, GETDATE(), GETDATE()),
    (@Vehicle7, '1HGBH41JXMN109192', 'EV-007', 'Volkswagen ID.4', 2023, 'Gray', 1, '2024-04-08', 17000, @Group4, GETDATE(), GETDATE()),
    (@Vehicle8, '1HGBH41JXMN109193', 'EV-008', 'Ford Mustang Mach-E', 2023, 'Orange', 1, '2024-04-14', 11000, @Group4, GETDATE(), GETDATE()),
    (@Vehicle9, '1HGBH41JXMN109194', 'EV-009', 'Kia Niro EV', 2023, 'Yellow', 1, '2024-04-16', 19000, @Group5, GETDATE(), GETDATE()),
    (@Vehicle10, '1HGBH41JXMN109195', 'EV-010', 'Polestar 2', 2023, 'Purple', 1, '2024-04-11', 15000, @Group5, GETDATE(), GETDATE())

-- 4. Create Group Members
PRINT 'Creating Group Members...'

INSERT INTO GroupMembers (Id, GroupId, UserId, SharePercentage, RoleInGroup, JoinedAt, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Group1, @User1, 0.2500, 0, '2024-01-01', GETDATE(), GETDATE()),
    (NEWID(), @Group1, @User2, 0.2500, 0, '2024-01-01', GETDATE(), GETDATE()),
    (NEWID(), @Group1, @User3, 0.2500, 0, '2024-01-01', GETDATE(), GETDATE()),
    (NEWID(), @Group1, @User4, 0.2500, 0, '2024-01-01', GETDATE(), GETDATE()),
    (NEWID(), @Group2, @User5, 0.3333, 0, '2024-02-01', GETDATE(), GETDATE()),
    (NEWID(), @Group2, @User6, 0.3333, 0, '2024-02-01', GETDATE(), GETDATE()),
    (NEWID(), @Group2, @User7, 0.3334, 0, '2024-02-01', GETDATE(), GETDATE()),
    (NEWID(), @Group3, @User8, 0.5000, 0, '2024-03-01', GETDATE(), GETDATE()),
    (NEWID(), @Group3, @User9, 0.5000, 0, '2024-03-01', GETDATE(), GETDATE()),
    (NEWID(), @Group4, @User10, 1.0000, 0, '2024-04-01', GETDATE(), GETDATE())

-- 5. Create Bookings
PRINT 'Creating Bookings...'

INSERT INTO Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, Notes, Purpose, IsEmergency, Priority, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Vehicle1, @Group1, @User1, '2024-05-01 08:00:00', '2024-05-01 12:00:00', 4, 85, 'Business meeting', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle1, @Group1, @User2, '2024-05-02 09:00:00', '2024-05-02 15:00:00', 4, 90, 'Shopping trip', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle2, @Group1, @User3, '2024-05-03 10:00:00', '2024-05-03 16:00:00', 4, 80, 'Family visit', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle2, @Group1, @User4, '2024-05-04 07:00:00', '2024-05-04 11:00:00', 4, 95, 'Airport pickup', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle3, @Group2, @User5, '2024-05-05 08:30:00', '2024-05-05 14:30:00', 4, 88, 'Weekend trip', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle3, @Group2, @User6, '2024-05-06 09:30:00', '2024-05-06 13:30:00', 4, 82, 'Doctor appointment', 'Personal', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle4, @Group2, @User7, '2024-05-07 11:00:00', '2024-05-07 17:00:00', 4, 87, 'Client meeting', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle5, @Group3, @User8, '2024-05-08 08:00:00', '2024-05-08 12:00:00', 4, 89, 'Gym visit', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle5, @Group3, @User9, '2024-05-09 10:00:00', '2024-05-09 16:00:00', 4, 83, 'City tour', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle6, @Group3, @User8, '2024-05-10 07:30:00', '2024-05-10 11:30:00', 4, 91, 'Work commute', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle7, @Group4, @User10, '2024-05-11 09:00:00', '2024-05-11 15:00:00', 4, 86, 'University visit', 'Personal', 0, 1, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle8, @Group4, @User10, '2024-05-12 08:00:00', '2024-05-12 14:00:00', 4, 84, 'Research trip', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle9, @Group5, @User1, '2024-05-13 10:00:00', '2024-05-13 16:00:00', 4, 88, 'Corporate event', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle10, @Group5, @User2, '2024-05-14 09:00:00', '2024-05-14 13:00:00', 4, 85, 'Client presentation', 'Business', 0, 2, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle10, @Group5, @User3, '2024-05-15 11:00:00', '2024-05-15 17:00:00', 4, 87, 'Team building', 'Business', 0, 1, GETDATE(), GETDATE())

-- 6. Create Check-ins
PRINT 'Creating Check-ins...'

DECLARE @Booking1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bookings WHERE VehicleId = @Vehicle1)
DECLARE @Booking2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bookings WHERE VehicleId = @Vehicle1 AND UserId = @User2)
DECLARE @Booking3 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bookings WHERE VehicleId = @Vehicle2)
DECLARE @Booking4 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bookings WHERE VehicleId = @Vehicle2 AND UserId = @User4)
DECLARE @Booking5 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bookings WHERE VehicleId = @Vehicle3)

INSERT INTO CheckIns (Id, BookingId, UserId, Type, Odometer, Notes, SignatureReference, CheckInTime, VehicleId, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Booking1, @User1, 0, 15000, 'Vehicle in good condition', 'SIG001', '2024-05-01 08:00:00', @Vehicle1, GETDATE(), GETDATE()),
    (NEWID(), @Booking1, @User1, 1, 15050, 'Trip completed successfully', 'SIG002', '2024-05-01 12:00:00', @Vehicle1, GETDATE(), GETDATE()),
    (NEWID(), @Booking2, @User2, 0, 15050, 'Clean vehicle', 'SIG003', '2024-05-02 09:00:00', @Vehicle1, GETDATE(), GETDATE()),
    (NEWID(), @Booking2, @User2, 1, 15120, 'Great trip', 'SIG004', '2024-05-02 15:00:00', @Vehicle1, GETDATE(), GETDATE()),
    (NEWID(), @Booking3, @User3, 0, 12000, 'Ready to go', 'SIG005', '2024-05-03 10:00:00', @Vehicle2, GETDATE(), GETDATE()),
    (NEWID(), @Booking3, @User3, 1, 12080, 'Smooth ride', 'SIG006', '2024-05-03 16:00:00', @Vehicle2, GETDATE(), GETDATE()),
    (NEWID(), @Booking4, @User4, 0, 12080, 'Perfect condition', 'SIG007', '2024-05-04 07:00:00', @Vehicle2, GETDATE(), GETDATE()),
    (NEWID(), @Booking4, @User4, 1, 12140, 'Mission accomplished', 'SIG008', '2024-05-04 11:00:00', @Vehicle2, GETDATE(), GETDATE()),
    (NEWID(), @Booking5, @User5, 0, 18000, 'Excellent vehicle', 'SIG009', '2024-05-05 08:30:00', @Vehicle3, GETDATE(), GETDATE()),
    (NEWID(), @Booking5, @User5, 1, 18060, 'Wonderful experience', 'SIG010', '2024-05-05 14:30:00', @Vehicle3, GETDATE(), GETDATE())

-- 7. Create Analytics Snapshots
PRINT 'Creating Analytics Snapshots...'

INSERT INTO AnalyticsSnapshots (Id, GroupId, VehicleId, SnapshotDate, Period, TotalDistance, TotalBookings, TotalUsageHours, ActiveUsers, TotalRevenue, TotalExpenses, NetProfit, AverageCostPerHour, AverageCostPerKm, UtilizationRate, MaintenanceEfficiency, UserSatisfactionScore, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Group1, @Vehicle1, '2024-01-01', 2, 5000, 15, 120, 4, 25000.00, 5000.00, 20000.00, 208.33, 5.00, 0.75, 0.85, 4.5, GETDATE(), GETDATE()),
    (NEWID(), @Group1, @Vehicle2, '2024-01-01', 2, 4800, 12, 100, 4, 22000.00, 4500.00, 17500.00, 220.00, 4.58, 0.70, 0.88, 4.3, GETDATE(), GETDATE()),
    (NEWID(), @Group2, @Vehicle3, '2024-02-01', 2, 5200, 18, 140, 3, 28000.00, 5500.00, 22500.00, 200.00, 5.38, 0.80, 0.82, 4.6, GETDATE(), GETDATE()),
    (NEWID(), @Group2, @Vehicle4, '2024-02-01', 2, 4600, 14, 110, 3, 26000.00, 4800.00, 21200.00, 236.36, 5.65, 0.75, 0.90, 4.4, GETDATE(), GETDATE()),
    (NEWID(), @Group3, @Vehicle5, '2024-03-01', 2, 5400, 20, 150, 2, 30000.00, 6000.00, 24000.00, 200.00, 5.56, 0.85, 0.87, 4.7, GETDATE(), GETDATE()),
    (NEWID(), @Group3, @Vehicle6, '2024-03-01', 2, 5100, 16, 125, 2, 28000.00, 5200.00, 22800.00, 224.00, 5.49, 0.78, 0.89, 4.5, GETDATE(), GETDATE()),
    (NEWID(), @Group4, @Vehicle7, '2024-04-01', 2, 5800, 22, 160, 1, 32000.00, 6500.00, 25500.00, 200.00, 5.52, 0.90, 0.84, 4.8, GETDATE(), GETDATE()),
    (NEWID(), @Group4, @Vehicle8, '2024-04-01', 2, 5500, 18, 135, 1, 30000.00, 5800.00, 24200.00, 222.22, 5.45, 0.82, 0.86, 4.6, GETDATE(), GETDATE()),
    (NEWID(), @Group5, @Vehicle9, '2024-05-01', 2, 6000, 25, 180, 3, 35000.00, 7000.00, 28000.00, 194.44, 5.83, 0.95, 0.91, 4.9, GETDATE(), GETDATE()),
    (NEWID(), @Group5, @Vehicle10, '2024-05-01', 2, 5700, 20, 165, 3, 33000.00, 6200.00, 26800.00, 200.00, 5.79, 0.88, 0.89, 4.7, GETDATE(), GETDATE())

-- 8. Create User Analytics
PRINT 'Creating User Analytics...'

INSERT INTO UserAnalytics (Id, UserId, GroupId, PeriodStart, PeriodEnd, Period, TotalBookings, TotalUsageHours, TotalDistance, OwnershipShare, UsageShare, TotalPaid, TotalOwed, NetBalance, BookingSuccessRate, Cancellations, NoShows, PunctualityScore, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @User1, @Group1, '2024-01-01', '2024-01-31', 2, 15, 120, 5000, 0.2500, 0.3000, 2500.00, 2000.00, 500.00, 0.95, 1, 0, 4.8, GETDATE(), GETDATE()),
    (NEWID(), @User2, @Group1, '2024-01-01', '2024-01-31', 2, 12, 100, 4800, 0.2500, 0.2800, 2200.00, 1800.00, 400.00, 0.92, 0, 1, 4.6, GETDATE(), GETDATE()),
    (NEWID(), @User3, @Group1, '2024-01-01', '2024-01-31', 2, 18, 140, 5200, 0.2500, 0.3200, 2800.00, 2200.00, 600.00, 0.94, 1, 0, 4.7, GETDATE(), GETDATE()),
    (NEWID(), @User4, @Group1, '2024-01-01', '2024-01-31', 2, 14, 110, 4600, 0.2500, 0.2600, 2300.00, 1900.00, 400.00, 0.93, 0, 0, 4.9, GETDATE(), GETDATE()),
    (NEWID(), @User5, @Group2, '2024-02-01', '2024-02-29', 2, 20, 150, 5400, 0.3333, 0.3500, 3000.00, 2500.00, 500.00, 0.96, 0, 0, 4.8, GETDATE(), GETDATE()),
    (NEWID(), @User6, @Group2, '2024-02-01', '2024-02-29', 2, 16, 125, 5100, 0.3333, 0.3200, 2800.00, 2300.00, 500.00, 0.94, 1, 0, 4.5, GETDATE(), GETDATE()),
    (NEWID(), @User7, @Group2, '2024-02-01', '2024-02-29', 2, 22, 160, 5800, 0.3334, 0.3800, 3200.00, 2700.00, 500.00, 0.95, 0, 0, 4.7, GETDATE(), GETDATE()),
    (NEWID(), @User8, @Group3, '2024-03-01', '2024-03-31', 2, 25, 180, 6000, 0.5000, 0.4500, 3500.00, 3000.00, 500.00, 0.97, 0, 0, 4.9, GETDATE(), GETDATE()),
    (NEWID(), @User9, @Group3, '2024-03-01', '2024-03-31', 2, 20, 165, 5700, 0.5000, 0.4000, 3300.00, 2800.00, 500.00, 0.95, 1, 0, 4.6, GETDATE(), GETDATE()),
    (NEWID(), @User10, @Group4, '2024-04-01', '2024-04-30', 2, 18, 135, 5500, 1.0000, 1.0000, 3000.00, 2500.00, 500.00, 0.94, 0, 0, 4.8, GETDATE(), GETDATE())

-- 9. Create Vehicle Analytics
PRINT 'Creating Vehicle Analytics...'

INSERT INTO VehicleAnalytics (Id, VehicleId, GroupId, PeriodStart, PeriodEnd, Period, TotalDistance, TotalBookings, TotalUsageHours, UtilizationRate, AvailabilityRate, Revenue, MaintenanceCost, OperatingCost, NetProfit, CostPerKm, CostPerHour, MaintenanceEvents, Breakdowns, ReliabilityScore, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Vehicle1, @Group1, '2024-01-01', '2024-01-31', 2, 5000, 15, 120, 0.75, 0.90, 25000.00, 2000.00, 1000.00, 22000.00, 0.60, 25.00, 2, 0, 0.95, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle2, @Group1, '2024-01-01', '2024-01-31', 2, 4800, 12, 100, 0.70, 0.85, 22000.00, 1800.00, 900.00, 19300.00, 0.56, 27.00, 1, 0, 0.92, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle3, @Group2, '2024-02-01', '2024-02-29', 2, 5200, 18, 140, 0.80, 0.88, 28000.00, 2200.00, 1100.00, 24700.00, 0.63, 23.57, 2, 0, 0.94, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle4, @Group2, '2024-02-01', '2024-02-29', 2, 4600, 14, 110, 0.75, 0.82, 26000.00, 2000.00, 1000.00, 23000.00, 0.65, 27.27, 1, 0, 0.93, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle5, @Group3, '2024-03-01', '2024-03-31', 2, 5400, 20, 150, 0.85, 0.92, 30000.00, 2400.00, 1200.00, 26400.00, 0.67, 24.00, 2, 0, 0.96, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle6, @Group3, '2024-03-01', '2024-03-31', 2, 5100, 16, 125, 0.78, 0.86, 28000.00, 2100.00, 1050.00, 24850.00, 0.62, 25.20, 1, 0, 0.94, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle7, @Group4, '2024-04-01', '2024-04-30', 2, 5800, 22, 160, 0.90, 0.95, 32000.00, 2600.00, 1300.00, 28100.00, 0.67, 23.75, 2, 0, 0.97, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle8, @Group4, '2024-04-01', '2024-04-30', 2, 5500, 18, 135, 0.82, 0.88, 30000.00, 2300.00, 1150.00, 26550.00, 0.63, 25.56, 1, 0, 0.95, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle9, @Group5, '2024-05-01', '2024-05-31', 2, 6000, 25, 180, 0.95, 0.98, 35000.00, 2800.00, 1400.00, 30800.00, 0.70, 23.33, 2, 0, 0.98, GETDATE(), GETDATE()),
    (NEWID(), @Vehicle10, @Group5, '2024-05-01', '2024-05-31', 2, 5700, 20, 165, 0.88, 0.94, 33000.00, 2500.00, 1250.00, 29250.00, 0.66, 25.45, 1, 0, 0.96, GETDATE(), GETDATE())

-- 10. Create Group Analytics
PRINT 'Creating Group Analytics...'

INSERT INTO GroupAnalytics (Id, GroupId, PeriodStart, PeriodEnd, Period, TotalMembers, ActiveMembers, NewMembers, LeftMembers, TotalRevenue, TotalExpenses, NetProfit, AverageMemberContribution, TotalBookings, TotalProposals, TotalVotes, ParticipationRate, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Group1, '2024-01-01', '2024-01-31', 2, 4, 4, 0, 0, 47000.00, 8000.00, 39000.00, 9750.00, 27, 3, 12, 0.85, GETDATE(), GETDATE()),
    (NEWID(), @Group2, '2024-02-01', '2024-02-29', 2, 3, 3, 0, 0, 54000.00, 10000.00, 44000.00, 14666.67, 32, 2, 8, 0.90, GETDATE(), GETDATE()),
    (NEWID(), @Group3, '2024-03-01', '2024-03-31', 2, 2, 2, 0, 0, 58000.00, 11000.00, 47000.00, 23500.00, 36, 1, 4, 0.95, GETDATE(), GETDATE()),
    (NEWID(), @Group4, '2024-04-01', '2024-04-30', 2, 1, 1, 0, 0, 62000.00, 12000.00, 50000.00, 50000.00, 40, 0, 0, 1.00, GETDATE(), GETDATE()),
    (NEWID(), @Group5, '2024-05-01', '2024-05-31', 2, 3, 3, 0, 0, 68000.00, 13000.00, 55000.00, 18333.33, 45, 2, 6, 0.88, GETDATE(), GETDATE())

-- 11. Create Payments
PRINT 'Creating Payments...'

INSERT INTO Payments (Id, InvoiceId, PayerId, Amount, Method, Status, TransactionReference, PaidAt, Notes, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), NEWID(), @User1, 2500.00, 'Credit Card', 'Completed', 'TXN001', '2024-05-01 10:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User2, 2200.00, 'PayPal', 'Completed', 'TXN002', '2024-05-02 11:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User3, 2800.00, 'Credit Card', 'Completed', 'TXN003', '2024-05-03 12:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User4, 2300.00, 'Bank Transfer', 'Completed', 'TXN004', '2024-05-04 13:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User5, 3000.00, 'Credit Card', 'Completed', 'TXN005', '2024-05-05 14:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User6, 2800.00, 'PayPal', 'Completed', 'TXN006', '2024-05-06 15:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User7, 3200.00, 'Credit Card', 'Completed', 'TXN007', '2024-05-07 16:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User8, 3500.00, 'Bank Transfer', 'Completed', 'TXN008', '2024-05-08 17:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User9, 3300.00, 'Credit Card', 'Completed', 'TXN009', '2024-05-09 18:00:00', 'Monthly contribution', GETDATE(), GETDATE()),
    (NEWID(), NEWID(), @User10, 3000.00, 'PayPal', 'Completed', 'TXN010', '2024-05-10 19:00:00', 'Monthly contribution', GETDATE(), GETDATE())

-- 12. Create Ledger Entries
PRINT 'Creating Ledger Entries...'

INSERT INTO LedgerEntries (Id, GroupId, Amount, Type, BalanceAfter, Description, Reference, RelatedEntityId, RelatedEntityType, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Group1, 2500.00, 'Credit', 2500.00, 'User contribution from John Smith', 'CONT001', @User1, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group1, 2200.00, 'Credit', 4700.00, 'User contribution from Sarah Johnson', 'CONT002', @User2, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group1, 2800.00, 'Credit', 7500.00, 'User contribution from Mike Wilson', 'CONT003', @User3, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group1, 2300.00, 'Credit', 9800.00, 'User contribution from Emily Davis', 'CONT004', @User4, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group1, -8000.00, 'Debit', 1800.00, 'Vehicle maintenance expenses', 'EXP001', @Vehicle1, 'Vehicle', GETDATE(), GETDATE()),
    (NEWID(), @Group2, 3000.00, 'Credit', 3000.00, 'User contribution from David Brown', 'CONT005', @User5, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group2, 2800.00, 'Credit', 5800.00, 'User contribution from Lisa Anderson', 'CONT006', @User6, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group2, 3200.00, 'Credit', 9000.00, 'User contribution from Tom Miller', 'CONT007', @User7, 'User', GETDATE(), GETDATE()),
    (NEWID(), @Group2, -10000.00, 'Debit', -1000.00, 'Vehicle maintenance expenses', 'EXP002', @Vehicle3, 'Vehicle', GETDATE(), GETDATE()),
    (NEWID(), @Group3, 3500.00, 'Credit', 3500.00, 'User contribution from Anna Garcia', 'CONT008', @User8, 'User', GETDATE(), GETDATE())

-- 13. Create Notifications
PRINT 'Creating Notifications...'

INSERT INTO Notifications (Id, UserId, GroupId, Title, Message, Type, Priority, Status, ReadAt, ScheduledFor, ActionUrl, ActionText, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @User1, @Group1, 'Booking Confirmed', 'Your booking for Tesla Model 3 has been confirmed', 'Booking', 'High', 'Read', '2024-05-01 08:00:00', '2024-05-01 08:00:00', '/bookings/123', 'View Booking', GETDATE(), GETDATE()),
    (NEWID(), @User2, @Group1, 'Payment Due', 'Your monthly contribution is due', 'Payment', 'High', 'Unread', NULL, '2024-05-15 00:00:00', '/payments', 'Pay Now', GETDATE(), GETDATE()),
    (NEWID(), @User3, @Group1, 'Vehicle Available', 'BMW i3 is now available for booking', 'Vehicle', 'Medium', 'Read', '2024-05-03 10:00:00', '2024-05-03 10:00:00', '/vehicles/456', 'Book Now', GETDATE(), GETDATE()),
    (NEWID(), @User4, @Group1, 'Group Meeting', 'Monthly group meeting scheduled', 'Meeting', 'Medium', 'Unread', NULL, '2024-05-20 18:00:00', '/meetings/789', 'Join Meeting', GETDATE(), GETDATE()),
    (NEWID(), @User5, @Group2, 'Maintenance Complete', 'Vehicle maintenance has been completed', 'Maintenance', 'Low', 'Read', '2024-05-05 14:00:00', '2024-05-05 14:00:00', '/vehicles/101', 'View Details', GETDATE(), GETDATE()),
    (NEWID(), @User6, @Group2, 'New Proposal', 'A new proposal has been submitted for voting', 'Proposal', 'High', 'Unread', NULL, '2024-05-12 09:00:00', '/proposals/202', 'Vote Now', GETDATE(), GETDATE()),
    (NEWID(), @User7, @Group2, 'Booking Reminder', 'Your booking starts in 1 hour', 'Booking', 'High', 'Read', '2024-05-07 10:00:00', '2024-05-07 10:00:00', '/bookings/303', 'View Booking', GETDATE(), GETDATE()),
    (NEWID(), @User8, @Group3, 'Expense Approved', 'Your expense claim has been approved', 'Expense', 'Medium', 'Read', '2024-05-08 16:00:00', '2024-05-08 16:00:00', '/expenses/404', 'View Details', GETDATE(), GETDATE()),
    (NEWID(), @User9, @Group3, 'Group Update', 'Group statistics updated', 'Update', 'Low', 'Unread', NULL, '2024-05-10 12:00:00', '/analytics', 'View Analytics', GETDATE(), GETDATE()),
    (NEWID(), @User10, @Group4, 'Welcome', 'Welcome to the group!', 'Welcome', 'Medium', 'Read', '2024-05-11 08:00:00', '2024-05-11 08:00:00', '/dashboard', 'Go to Dashboard', GETDATE(), GETDATE())

-- 14. Create Proposals
PRINT 'Creating Proposals...'

INSERT INTO Proposals (Id, GroupId, CreatedBy, Title, Description, Type, Amount, Status, VotingStartDate, VotingEndDate, RequiredMajority, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Group1, @User1, 'New Vehicle Purchase', 'Proposal to purchase a new Tesla Model Y', 0, 50000.00, 0, '2024-05-15', '2024-05-25', 0.75, GETDATE(), GETDATE()),
    (NEWID(), @Group1, @User2, 'Maintenance Schedule', 'Proposal to update maintenance schedule', 2, 0.00, 1, '2024-04-15', '2024-04-25', 0.50, GETDATE(), GETDATE()),
    (NEWID(), @Group2, @User5, 'Insurance Update', 'Proposal to update insurance coverage', 0, 2000.00, 0, '2024-05-10', '2024-05-20', 0.67, GETDATE(), GETDATE()),
    (NEWID(), @Group3, @User8, 'Charging Station', 'Proposal to install new charging station', 0, 15000.00, 2, '2024-05-20', '2024-05-30', 0.50, GETDATE(), GETDATE()),
    (NEWID(), @Group5, @User1, 'Fleet Expansion', 'Proposal to expand fleet with 2 new vehicles', 0, 80000.00, 3, '2024-06-01', '2024-06-15', 0.67, GETDATE(), GETDATE())

-- 15. Create Votes
PRINT 'Creating Votes...'

DECLARE @Proposal1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Proposals WHERE Title = 'New Vehicle Purchase')
DECLARE @Proposal2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Proposals WHERE Title = 'Insurance Update')

INSERT INTO Votes (Id, ProposalId, VoterId, Weight, Choice, Comment, VotedAt, CreatedAt, UpdatedAt)
VALUES 
    (NEWID(), @Proposal1, @User1, 1.0, 'Yes', 'Great idea for expanding our fleet', '2024-05-16 10:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal1, @User2, 1.0, 'Yes', 'I support this proposal', '2024-05-16 11:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal1, @User3, 1.0, 'No', 'Too expensive for current budget', '2024-05-16 12:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal1, @User4, 1.0, 'Yes', 'Long-term investment is worth it', '2024-05-16 13:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal2, @User5, 1.0, 'Yes', 'Better coverage is important', '2024-05-11 09:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal2, @User6, 1.0, 'Yes', 'Agree with the proposal', '2024-05-11 10:00:00', GETDATE(), GETDATE()),
    (NEWID(), @Proposal2, @User7, 1.0, 'No', 'Current coverage is sufficient', '2024-05-11 11:00:00', GETDATE(), GETDATE())

PRINT 'Sample data creation completed successfully!'
PRINT '======================================================='
PRINT 'Data Summary:'
PRINT '- Users: 10 records'
PRINT '- Ownership Groups: 5 records'
PRINT '- Vehicles: 10 records'
PRINT '- Group Members: 10 records'
PRINT '- Bookings: 15 records'
PRINT '- Check-ins: 10 records'
PRINT '- Analytics Snapshots: 10 records'
PRINT '- User Analytics: 10 records'
PRINT '- Vehicle Analytics: 10 records'
PRINT '- Group Analytics: 5 records'
PRINT '- Payments: 10 records'
PRINT '- Ledger Entries: 10 records'
PRINT '- Notifications: 10 records'
PRINT '- Proposals: 5 records'
PRINT '- Votes: 7 records'
PRINT ''
PRINT 'You can now test the Analytics API endpoints with this comprehensive sample data!'
PRINT 'Use the following test scenarios:'
PRINT '1. Test date range filtering: startDate=2024-01-01&endDate=2024-05-31'
PRINT '2. Test group filtering: groupId=<specific-group-id>'
PRINT '3. Test all endpoints with proper JWT authentication'
PRINT ''
PRINT 'Sample data includes:'
PRINT '- Complete ownership group structure'
PRINT '- User and vehicle relationships'
PRINT '- Booking and check-in data'
PRINT '- Financial transactions and ledger entries'
PRINT '- Analytics data for all entities'
PRINT '- Notifications and proposals'
PRINT '- Voting and group participation data'
GO
