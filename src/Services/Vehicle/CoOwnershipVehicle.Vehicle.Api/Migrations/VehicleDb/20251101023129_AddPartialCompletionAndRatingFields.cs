using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class AddPartialCompletionAndRatingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionPercentage",
                table: "MaintenanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ServiceProviderRating",
                table: "MaintenanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceProviderReview",
                table: "MaintenanceRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionPercentage",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceProviderRating",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceProviderReview",
                table: "MaintenanceRecords");
        }
    }
}
