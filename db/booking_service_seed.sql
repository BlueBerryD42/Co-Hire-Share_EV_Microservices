/*
    Booking Service SQL Server seed script
    --------------------------------------
    - Creates minimal schema for vehicles, groups, users, bookings, templates, recurring bookings,
      check-ins, damage reports, late return fees, notification preferences.
    - Inserts deterministic sample rows that align with the FE mock GUIDs.
    - Safe to run on SQL Server 2019+. Wraps DDL in IF EXISTS checks to keep script idempotent.
*/
USE [cohire_share_ev];
GO

IF OBJECT_ID('dbo.DamageReports', 'U') IS NOT NULL DROP TABLE dbo.DamageReports;
IF OBJECT_ID('dbo.CheckIns', 'U') IS NOT NULL DROP TABLE dbo.CheckIns;
IF OBJECT_ID('dbo.LateReturnFees', 'U') IS NOT NULL DROP TABLE dbo.LateReturnFees;
IF OBJECT_ID('dbo.NotificationPreferences', 'U') IS NOT NULL DROP TABLE dbo.NotificationPreferences;
IF OBJECT_ID('dbo.RecurringBookings', 'U') IS NOT NULL DROP TABLE dbo.RecurringBookings;
IF OBJECT_ID('dbo.BookingTemplates', 'U') IS NOT NULL DROP TABLE dbo.BookingTemplates;
IF OBJECT_ID('dbo.Bookings', 'U') IS NOT NULL DROP TABLE dbo.Bookings;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Vehicles', 'U') IS NOT NULL DROP TABLE dbo.Vehicles;
IF OBJECT_ID('dbo.OwnershipGroups', 'U') IS NOT NULL DROP TABLE dbo.OwnershipGroups;
GO

CREATE TABLE dbo.OwnershipGroups
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Users
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    GroupId UNIQUEIDENTIFIER NOT NULL,
    FirstName NVARCHAR(80) NOT NULL,
    LastName NVARCHAR(80) NOT NULL,
    Email NVARCHAR(180) NOT NULL,
    Role NVARCHAR(40) NOT NULL,
    CONSTRAINT FK_Users_Groups FOREIGN KEY (GroupId) REFERENCES dbo.OwnershipGroups(Id)
);

CREATE TABLE dbo.Vehicles
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    GroupId UNIQUEIDENTIFIER NOT NULL,
    DisplayName NVARCHAR(150) NOT NULL,
    PlateNumber NVARCHAR(50) NOT NULL,
    Model NVARCHAR(150) NOT NULL,
    YearOfManufacture SMALLINT NOT NULL,
    CONSTRAINT FK_Vehicles_Groups FOREIGN KEY (GroupId) REFERENCES dbo.OwnershipGroups(Id)
);

CREATE TABLE dbo.Bookings
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    GroupId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    StartAt DATETIME2 NOT NULL,
    EndAt DATETIME2 NOT NULL,
    Status INT NOT NULL,
    PriorityScore DECIMAL(10,4) NOT NULL DEFAULT 0,
    Notes NVARCHAR(500) NULL,
    Purpose NVARCHAR(200) NULL,
    IsEmergency BIT NOT NULL DEFAULT 0,
    Priority INT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Bookings_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(Id),
    CONSTRAINT FK_Bookings_Groups FOREIGN KEY (GroupId) REFERENCES dbo.OwnershipGroups(Id),
    CONSTRAINT FK_Bookings_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);

CREATE TABLE dbo.BookingTemplates
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    VehicleId UNIQUEIDENTIFIER NULL,
    DurationMinutes INT NOT NULL,
    PreferredStart NVARCHAR(5) NOT NULL,
    Purpose NVARCHAR(200) NULL,
    Notes NVARCHAR(1000) NULL,
    Priority INT NOT NULL DEFAULT 1,
    UsageCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Templates_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);

CREATE TABLE dbo.RecurringBookings
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    GroupId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Pattern NVARCHAR(40) NOT NULL,
    IntervalValue INT NOT NULL DEFAULT(1),
    DaysOfWeek NVARCHAR(100) NULL,
    StartTime NVARCHAR(5) NOT NULL,
    EndTime NVARCHAR(5) NOT NULL,
    RecurrenceStart DATETIME2 NOT NULL,
    RecurrenceEnd DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    Notes NVARCHAR(500) NULL,
    Purpose NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.NotificationPreferences
(
    UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EnableReminders BIT NOT NULL DEFAULT 1,
    EnableEmail BIT NOT NULL DEFAULT 1,
    EnableSms BIT NOT NULL DEFAULT 0,
    PreferredTimeZoneId NVARCHAR(100) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Preferences_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);

CREATE TABLE dbo.CheckIns
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    BookingId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    CheckInType NVARCHAR(20) NOT NULL,
    Odometer INT NOT NULL,
    BatteryPercent INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes NVARCHAR(1000) NULL,
    CONSTRAINT FK_CheckIns_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id)
);

CREATE TABLE dbo.DamageReports
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CheckInId UNIQUEIDENTIFIER NOT NULL,
    BookingId UNIQUEIDENTIFIER NOT NULL,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    Severity NVARCHAR(20) NOT NULL,
    Location NVARCHAR(50) NOT NULL,
    Description NVARCHAR(1000) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Open',
    EstimatedCost DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_DamageReports_CheckIns FOREIGN KEY (CheckInId) REFERENCES dbo.CheckIns(Id)
);

CREATE TABLE dbo.LateReturnFees
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    BookingId UNIQUEIDENTIFIER NOT NULL,
    CheckInId UNIQUEIDENTIFIER NOT NULL,
    GroupId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    LateDurationMinutes INT NOT NULL,
    FeeAmount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_LateFees_Bookings FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(Id)
);
GO

-- Seed base entities
DECLARE @GroupId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-0000000000AB';
DECLARE @UserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-0000000000AA';
DECLARE @VehicleTesla UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @VehicleKia UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Booking1 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000101';
DECLARE @Booking2 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000102';

INSERT INTO dbo.OwnershipGroups (Id, Name, Description)
VALUES (@GroupId, 'Saigon EV Collective', 'Demo group for booking service');

INSERT INTO dbo.Users (Id, GroupId, FirstName, LastName, Email, Role)
VALUES (@UserId, @GroupId, N'Anh', N'Nguyen', 'anh.nguyen@example.com', 'GroupAdmin');

INSERT INTO dbo.Vehicles (Id, GroupId, DisplayName, PlateNumber, Model, YearOfManufacture)
VALUES
(@VehicleTesla, @GroupId, 'Tesla Model 3 Performance', '51A-123.45', 'Model 3 Performance', 2023),
(@VehicleKia, @GroupId, 'Kia EV6 GT-Line', '51A-678.90', 'EV6 GT-Line', 2022);

INSERT INTO dbo.Bookings (Id, VehicleId, GroupId, UserId, StartAt, EndAt, Status, PriorityScore, Notes, Purpose, IsEmergency, Priority)
VALUES
(@Booking1, @VehicleTesla, @GroupId, @UserId, '2025-03-18T08:00:00Z', '2025-03-18T15:00:00Z', 2, 0.42, N'Morning client demo', N'Business demo', 0, 1),
(@Booking2, @VehicleKia, @GroupId, @UserId, '2025-03-19T06:00:00Z', '2025-03-19T22:00:00Z', 3, 0.75, N'Road trip south', N'Personal trip', 0, 1);

INSERT INTO dbo.BookingTemplates (Id, UserId, Name, VehicleId, DurationMinutes, PreferredStart, Purpose, Notes, Priority, UsageCount)
VALUES
('00000000-0000-0000-0000-00000000T001', @UserId, 'Morning business demo', @VehicleTesla, 240, '08:00', 'Business demo', 'Requires detailed cleaning before hand-off.', 2, 5),
('00000000-0000-0000-0000-00000000T002', @UserId, 'Weekend family trip', @VehicleKia, 600, '09:00', 'Family trip', 'Pack child seat in trunk.', 1, 2);

INSERT INTO dbo.RecurringBookings (Id, VehicleId, GroupId, UserId, Pattern, IntervalValue, DaysOfWeek, StartTime, EndTime, RecurrenceStart, RecurrenceEnd, Status, Notes, Purpose)
VALUES
('00000000-0000-0000-0000-00000000R001', @VehicleTesla, @GroupId, @UserId, 'Weekly', 1, 'Monday,Wednesday', '08:00', '12:00', '2025-03-01', NULL, 'Active', 'Weekly commuting slot', 'Business commute');

INSERT INTO dbo.NotificationPreferences (UserId, EnableReminders, EnableEmail, EnableSms, PreferredTimeZoneId)
VALUES
(@UserId, 1, 1, 0, 'Asia/Ho_Chi_Minh');

INSERT INTO dbo.CheckIns (Id, BookingId, UserId, VehicleId, CheckInType, Odometer, BatteryPercent, CreatedAt, Notes)
VALUES
('00000000-0000-0000-0000-00000000C001', @Booking1, @UserId, @VehicleTesla, 'CheckOut', 32110, 96, '2025-03-18T07:45:00Z', N'Pre-trip photos captured.'),
('00000000-0000-0000-0000-00000000C002', @Booking1, @UserId, @VehicleTesla, 'CheckIn', 32320, 48, '2025-03-18T15:30:00Z', N'Trip completed, slight scratch rear bumper.');

INSERT INTO dbo.DamageReports (Id, CheckInId, BookingId, VehicleId, Severity, Location, Description, Status, EstimatedCost)
VALUES
('00000000-0000-0000-0000-00000000D001', '00000000-0000-0000-0000-00000000C002', @Booking1, @VehicleTesla, 'Minor', 'Rear bumper', N'Light scratch detected during inspection.', 'Open', 200.00);

INSERT INTO dbo.LateReturnFees (Id, BookingId, CheckInId, GroupId, UserId, VehicleId, LateDurationMinutes, FeeAmount, Status)
VALUES
('00000000-0000-0000-0000-00000000F001', @Booking2, '00000000-0000-0000-0000-00000000C002', @GroupId, @UserId, @VehicleKia, 45, 75.00, 'Pending');
GO

PRINT 'Booking service seed data loaded successfully.';
