using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class RemoveCrossServiceForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove foreign keys from Bookings table
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_OwnershipGroups_GroupId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Vehicles_VehicleId",
                table: "Bookings");

            // Remove foreign keys from CheckIn table
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Users_UserId",
                table: "CheckIn");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn");

            // Remove foreign keys from RecurringBookings table
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBookings_OwnershipGroups_GroupId",
                table: "RecurringBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBookings_Users_UserId",
                table: "RecurringBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBookings_Vehicles_VehicleId",
                table: "RecurringBookings");

            // Remove foreign keys from BookingTemplates table
            migrationBuilder.DropForeignKey(
                name: "FK_BookingTemplates_Users_UserId",
                table: "BookingTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingTemplates_Vehicles_VehicleId",
                table: "BookingTemplates");

            // Remove foreign keys from LateReturnFee table
            migrationBuilder.DropForeignKey(
                name: "FK_LateReturnFee_OwnershipGroups_GroupId",
                table: "LateReturnFee");

            migrationBuilder.DropForeignKey(
                name: "FK_LateReturnFee_Users_UserId",
                table: "LateReturnFee");

            migrationBuilder.DropForeignKey(
                name: "FK_LateReturnFee_Vehicles_VehicleId",
                table: "LateReturnFee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore foreign keys for LateReturnFee table
            migrationBuilder.AddForeignKey(
                name: "FK_LateReturnFee_Vehicles_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LateReturnFee_Users_UserId",
                table: "LateReturnFee",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LateReturnFee_OwnershipGroups_GroupId",
                table: "LateReturnFee",
                column: "GroupId",
                principalTable: "OwnershipGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Restore foreign keys for BookingTemplates table
            migrationBuilder.AddForeignKey(
                name: "FK_BookingTemplates_Vehicles_VehicleId",
                table: "BookingTemplates",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingTemplates_Users_UserId",
                table: "BookingTemplates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Restore foreign keys for RecurringBookings table
            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBookings_Vehicles_VehicleId",
                table: "RecurringBookings",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBookings_Users_UserId",
                table: "RecurringBookings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBookings_OwnershipGroups_GroupId",
                table: "RecurringBookings",
                column: "GroupId",
                principalTable: "OwnershipGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Restore foreign keys for CheckIn table
            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Users_UserId",
                table: "CheckIn",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Restore foreign keys for Bookings table
            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Vehicles_VehicleId",
                table: "Bookings",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_OwnershipGroups_GroupId",
                table: "Bookings",
                column: "GroupId",
                principalTable: "OwnershipGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

