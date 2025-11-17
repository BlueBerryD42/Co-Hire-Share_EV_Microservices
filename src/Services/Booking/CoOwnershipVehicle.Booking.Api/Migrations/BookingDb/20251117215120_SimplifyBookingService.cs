using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class SimplifyBookingService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BookingTemplates_BookingTemplateId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_RecurringBookings_RecurringBookingId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "BookingNotificationPreference");

            migrationBuilder.DropTable(
                name: "BookingTemplates");

            migrationBuilder.DropTable(
                name: "DamageReport");

            migrationBuilder.DropTable(
                name: "LateReturnFee");

            migrationBuilder.DropTable(
                name: "MaintenanceBlocks");

            migrationBuilder.DropTable(
                name: "RecurringBookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingTemplateId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RecurringBookingId",
                table: "Bookings");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "CheckInPhoto",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsLateReturn",
                table: "CheckIn",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresDamageReview",
                table: "Bookings",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceKm",
                table: "Bookings",
                type: "decimal(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TripFeeAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VehicleStatus",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TripFeeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "VehicleStatus",
                table: "Bookings");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "CheckInPhoto",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsLateReturn",
                table: "CheckIn",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresDamageReview",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateTable(
                name: "BookingNotificationPreference",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnableEmail = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableReminders = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableSms = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PreferredTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNotificationPreference", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "BookingTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreferredStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DamageReport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamageReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DamageReport_CheckIn_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LateReturnFee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LateDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    OriginalFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaivedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateReturnFee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LateReturnFee_CheckIn_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "int", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastGeneratedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastGenerationRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Pattern = table.Column<int>(type: "int", nullable: false),
                    PausedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBookings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingTemplateId",
                table: "Bookings",
                column: "BookingTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RecurringBookingId",
                table: "Bookings",
                column: "RecurringBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplates_UserId",
                table: "BookingTemplates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplates_VehicleId",
                table: "BookingTemplates",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReport_BookingId",
                table: "DamageReport",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReport_CheckInId",
                table: "DamageReport",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReport_VehicleId",
                table: "DamageReport",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_BookingId",
                table: "LateReturnFee",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_CheckInId",
                table: "LateReturnFee",
                column: "CheckInId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_Status",
                table: "LateReturnFee",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_UserId",
                table: "LateReturnFee",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBlocks_MaintenanceScheduleId",
                table: "MaintenanceBlocks",
                column: "MaintenanceScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBlocks_Status",
                table: "MaintenanceBlocks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBlocks_VehicleId_StartTime_EndTime",
                table: "MaintenanceBlocks",
                columns: new[] { "VehicleId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookings_Status",
                table: "RecurringBookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookings_UserId",
                table: "RecurringBookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookings_VehicleId",
                table: "RecurringBookings",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BookingTemplates_BookingTemplateId",
                table: "Bookings",
                column: "BookingTemplateId",
                principalTable: "BookingTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_RecurringBookings_RecurringBookingId",
                table: "Bookings",
                column: "RecurringBookingId",
                principalTable: "RecurringBookings",
                principalColumn: "Id");
        }
    }
}
