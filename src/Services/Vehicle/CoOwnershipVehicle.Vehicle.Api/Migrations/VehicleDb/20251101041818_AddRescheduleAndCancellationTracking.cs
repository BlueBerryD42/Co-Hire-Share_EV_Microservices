using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class AddRescheduleAndCancellationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MaintenanceSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRescheduleReason",
                table: "MaintenanceSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastRescheduledBy",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalScheduledDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCount",
                table: "MaintenanceSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastRescheduleReason",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastRescheduledBy",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "OriginalScheduledDate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "RescheduleCount",
                table: "MaintenanceSchedules");
        }
    }
}
