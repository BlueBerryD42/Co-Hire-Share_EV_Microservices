using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class AddMaintenanceBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceBlocks_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceBlocks");
        }
    }
}
