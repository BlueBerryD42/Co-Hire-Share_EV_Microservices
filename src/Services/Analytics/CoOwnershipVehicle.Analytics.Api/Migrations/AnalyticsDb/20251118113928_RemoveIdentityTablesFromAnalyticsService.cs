using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Analytics.Api.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class RemoveIdentityTablesFromAnalyticsService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSignature");

            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAt",
                table: "CheckInPhoto",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "CheckInPhoto",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CheckInPhoto",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "CheckInPhoto",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "CheckInPhoto",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "CheckInPhoto",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "CheckInPhoto",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "CheckInPhoto",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingSuggestionFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuggestedEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSuggestionFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingTemplate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    PreferredStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTemplate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FairnessTrends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupFairnessScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FairnessTrends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LateReturnFee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LateDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CalculationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaivedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateReturnFee", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ServiceProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    OdometerReading = table.Column<int>(type: "int", nullable: true),
                    OdometerAtService = table.Column<int>(type: "int", nullable: true),
                    WorkPerformed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsUsed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsReplaced = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextServiceDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextServiceOdometer = table.Column<int>(type: "int", nullable: true),
                    ServiceProviderRating = table.Column<int>(type: "int", nullable: true),
                    ServiceProviderReview = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletionPercentage = table.Column<int>(type: "int", nullable: false),
                    PerformedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBooking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastGeneratedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastGenerationRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pattern = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RecurrenceStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBooking", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    TotalBookings = table.Column<int>(type: "int", nullable: false),
                    TotalUsageHours = table.Column<int>(type: "int", nullable: false),
                    TotalDistance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    OwnershipShare = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    UsageShare = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOwed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BookingSuccessRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Cancellations = table.Column<int>(type: "int", nullable: false),
                    NoShows = table.Column<int>(type: "int", nullable: false),
                    PunctualityScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAnalytics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingSuggestionFeedback_UserId_GroupId_SuggestedStart",
                table: "BookingSuggestionFeedback",
                columns: new[] { "UserId", "GroupId", "SuggestedStart" });

            migrationBuilder.CreateIndex(
                name: "IX_FairnessTrends_GroupId_PeriodStart_PeriodEnd",
                table: "FairnessTrends",
                columns: new[] { "GroupId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalytics_GroupId",
                table: "UserAnalytics",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalytics_PeriodStart",
                table: "UserAnalytics",
                column: "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalytics_UserId",
                table: "UserAnalytics",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingSuggestionFeedback");

            migrationBuilder.DropTable(
                name: "BookingTemplate");

            migrationBuilder.DropTable(
                name: "FairnessTrends");

            migrationBuilder.DropTable(
                name: "LateReturnFee");

            migrationBuilder.DropTable(
                name: "MaintenanceRecord");

            migrationBuilder.DropTable(
                name: "RecurringBooking");

            migrationBuilder.DropTable(
                name: "UserAnalytics");

            migrationBuilder.DropColumn(
                name: "CapturedAt",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "CheckInPhoto");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "CheckInPhoto");

            migrationBuilder.CreateTable(
                name: "DocumentSignature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSignature", x => x.Id);
                });
        }
    }
}
