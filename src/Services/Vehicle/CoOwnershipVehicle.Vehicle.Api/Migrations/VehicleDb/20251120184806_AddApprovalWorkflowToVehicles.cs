using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflowToVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Vehicles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "Vehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            // Update enum values: Shift existing values to make room for PendingApproval=0 and Rejected=5
            // Old: Available=0, InUse=1, Maintenance=2, Unavailable=3
            // New: PendingApproval=0, Available=1, InUse=2, Maintenance=3, Unavailable=4, Rejected=5
            migrationBuilder.Sql(@"
                UPDATE Vehicles 
                SET Status = CASE 
                    WHEN Status = 0 THEN 1  -- Available: 0 -> 1
                    WHEN Status = 1 THEN 2  -- InUse: 1 -> 2
                    WHEN Status = 2 THEN 3  -- Maintenance: 2 -> 3
                    WHEN Status = 3 THEN 4   -- Unavailable: 3 -> 4
                    ELSE Status
                END
            ");

            // Set SubmittedAt = CreatedAt for existing Available vehicles (now Status = 1)
            migrationBuilder.Sql(@"
                UPDATE Vehicles 
                SET SubmittedAt = CreatedAt 
                WHERE Status = 1 AND SubmittedAt IS NULL
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ReviewedBy",
                table: "Vehicles",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Status",
                table: "Vehicles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_SubmittedAt",
                table: "Vehicles",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert enum values: Shift back to original values
            // New: PendingApproval=0, Available=1, InUse=2, Maintenance=3, Unavailable=4, Rejected=5
            // Old: Available=0, InUse=1, Maintenance=2, Unavailable=3
            migrationBuilder.Sql(@"
                UPDATE Vehicles 
                SET Status = CASE 
                    WHEN Status = 1 THEN 0  -- Available: 1 -> 0
                    WHEN Status = 2 THEN 1  -- InUse: 2 -> 1
                    WHEN Status = 3 THEN 2  -- Maintenance: 3 -> 2
                    WHEN Status = 4 THEN 3  -- Unavailable: 4 -> 3
                    WHEN Status = 0 THEN 0  -- PendingApproval -> set to Available (0) for backward compatibility
                    WHEN Status = 5 THEN 0 -- Rejected -> set to Available (0) for backward compatibility
                    ELSE Status
                END
            ");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ReviewedBy",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Status",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_SubmittedAt",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Vehicles");
        }
    }
}
