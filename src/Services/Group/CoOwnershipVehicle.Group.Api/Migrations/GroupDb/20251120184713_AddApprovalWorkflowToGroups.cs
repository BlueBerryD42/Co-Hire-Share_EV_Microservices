using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Group.Api.Migrations.GroupDb
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflowToGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "OwnershipGroups",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "OwnershipGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "OwnershipGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "OwnershipGroups",
                type: "datetime2",
                nullable: true);

            // Update enum values: Shift existing values to make room for PendingApproval=0 and Rejected=4
            // Old: Active=0, Inactive=1, Dissolved=2
            // New: PendingApproval=0, Active=1, Inactive=2, Dissolved=3, Rejected=4
            migrationBuilder.Sql(@"
                UPDATE OwnershipGroups 
                SET Status = CASE 
                    WHEN Status = 0 THEN 1  -- Active: 0 -> 1
                    WHEN Status = 1 THEN 2  -- Inactive: 1 -> 2
                    WHEN Status = 2 THEN 3   -- Dissolved: 2 -> 3
                    ELSE Status
                END
            ");

            // Set SubmittedAt = CreatedAt for existing Active groups (now Status = 1)
            migrationBuilder.Sql(@"
                UPDATE OwnershipGroups 
                SET SubmittedAt = CreatedAt 
                WHERE Status = 1 AND SubmittedAt IS NULL
            ");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipGroups_ReviewedBy",
                table: "OwnershipGroups",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipGroups_Status",
                table: "OwnershipGroups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipGroups_SubmittedAt",
                table: "OwnershipGroups",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert enum values: Shift back to original values
            // New: PendingApproval=0, Active=1, Inactive=2, Dissolved=3, Rejected=4
            // Old: Active=0, Inactive=1, Dissolved=2
            migrationBuilder.Sql(@"
                UPDATE OwnershipGroups 
                SET Status = CASE 
                    WHEN Status = 1 THEN 0  -- Active: 1 -> 0
                    WHEN Status = 2 THEN 1  -- Inactive: 2 -> 1
                    WHEN Status = 3 THEN 2  -- Dissolved: 3 -> 2
                    WHEN Status = 0 THEN 0  -- PendingApproval -> set to Active (0) for backward compatibility
                    WHEN Status = 4 THEN 0 -- Rejected -> set to Active (0) for backward compatibility
                    ELSE Status
                END
            ");

            migrationBuilder.DropIndex(
                name: "IX_OwnershipGroups_ReviewedBy",
                table: "OwnershipGroups");

            migrationBuilder.DropIndex(
                name: "IX_OwnershipGroups_Status",
                table: "OwnershipGroups");

            migrationBuilder.DropIndex(
                name: "IX_OwnershipGroups_SubmittedAt",
                table: "OwnershipGroups");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "OwnershipGroups");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "OwnershipGroups");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "OwnershipGroups");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "OwnershipGroups");
        }
    }
}
