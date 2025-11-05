using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class AddRecurringBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn");

            migrationBuilder.AddColumn<bool>(
                name: "IsLateReturn",
                table: "CheckIn",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeAmount",
                table: "CheckIn",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LateReturnMinutes",
                table: "CheckIn",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureCapturedAt",
                table: "CheckIn",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureCertificateUrl",
                table: "CheckIn",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureDevice",
                table: "CheckIn",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureDeviceId",
                table: "CheckIn",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureHash",
                table: "CheckIn",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureIpAddress",
                table: "CheckIn",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SignatureMatchesPrevious",
                table: "CheckIn",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureMetadataJson",
                table: "CheckIn",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BookingTemplateId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyReason",
                table: "Bookings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalCheckoutReminderSentAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MissedCheckoutReminderSentAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreCheckoutReminderSentAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringBookingId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDamageReview",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BookingNotificationPreference",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnableReminders = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableEmail = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EnableSms = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PreferredTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
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
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    PreferredStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingTemplates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingTemplates_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DamageReport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
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
                    table.ForeignKey(
                        name: "FK_LateReturnFee_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBookings",
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
                    Interval = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DaysOfWeekMask = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RecurrenceStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBookings_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringBookings_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_LateReturnFee_GroupId",
                table: "LateReturnFee",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_Status",
                table: "LateReturnFee",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_UserId",
                table: "LateReturnFee",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookings_GroupId",
                table: "RecurringBookings",
                column: "GroupId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BookingTemplates_BookingTemplateId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_RecurringBookings_RecurringBookingId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn");

            migrationBuilder.DropTable(
                name: "BookingNotificationPreference");

            migrationBuilder.DropTable(
                name: "BookingTemplates");

            migrationBuilder.DropTable(
                name: "DamageReport");

            migrationBuilder.DropTable(
                name: "LateReturnFee");

            migrationBuilder.DropTable(
                name: "RecurringBookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingTemplateId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RecurringBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsLateReturn",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "LateFeeAmount",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "LateReturnMinutes",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureCapturedAt",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureCertificateUrl",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureDevice",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureDeviceId",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureHash",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureIpAddress",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureMatchesPrevious",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "SignatureMetadataJson",
                table: "CheckIn");

            migrationBuilder.DropColumn(
                name: "BookingTemplateId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EmergencyReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FinalCheckoutReminderSentAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MissedCheckoutReminderSentAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PreCheckoutReminderSentAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RecurringBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RequiresDamageReview",
                table: "Bookings");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
