using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class RemoveIdentityTablesFromVehicleService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_VehicleId",
                table: "MaintenanceRecords");

            migrationBuilder.RenameColumn(
                name: "ServiceDate",
                table: "MaintenanceRecords",
                newName: "ScheduledDate");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_ServiceDate",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_ScheduledDate");

            migrationBuilder.AlterColumn<int>(
                name: "OdometerReading",
                table: "MaintenanceRecords",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ActualDurationMinutes",
                table: "MaintenanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "MaintenanceRecords",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDurationMinutes",
                table: "MaintenanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "MaintenanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "MaintenanceRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdometerAtService",
                table: "MaintenanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartsUsed",
                table: "MaintenanceRecords",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "MaintenanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "MaintenanceRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceCompletedDate",
                table: "MaintenanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "MaintenanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateReturnFee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_CheckIn_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
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
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBooking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBooking_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_VehicleId_Status_ScheduledDate",
                table: "MaintenanceRecords",
                columns: new[] { "VehicleId", "Status", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_CheckInId",
                table: "LateReturnFee",
                column: "CheckInId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_VehicleId",
                table: "RecurringBooking",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LateReturnFee");

            migrationBuilder.DropTable(
                name: "RecurringBooking");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_VehicleId_Status_ScheduledDate",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ActualDurationMinutes",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "EstimatedDurationMinutes",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "OdometerAtService",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "PartsUsed",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceCompletedDate",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MaintenanceRecords");

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

            migrationBuilder.RenameColumn(
                name: "ScheduledDate",
                table: "MaintenanceRecords",
                newName: "ServiceDate");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_ScheduledDate",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_ServiceDate");

            migrationBuilder.AlterColumn<int>(
                name: "OdometerReading",
                table: "MaintenanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_VehicleId",
                table: "MaintenanceRecords",
                column: "VehicleId");
        }
    }
}
