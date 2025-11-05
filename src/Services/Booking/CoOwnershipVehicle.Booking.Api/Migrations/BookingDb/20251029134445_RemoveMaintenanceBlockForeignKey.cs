using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class RemoveMaintenanceBlockForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceBlocks_Vehicles_VehicleId",
                table: "MaintenanceBlocks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceBlocks_Vehicles_VehicleId",
                table: "MaintenanceBlocks",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
