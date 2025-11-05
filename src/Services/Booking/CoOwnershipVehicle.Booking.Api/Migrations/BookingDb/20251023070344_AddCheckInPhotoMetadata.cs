using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Booking.Api.Migrations.BookingDb
{
    /// <inheritdoc />
    public partial class AddCheckInPhotoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn");

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

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn");

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

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIn_Vehicles_VehicleId",
                table: "CheckIn",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");
        }
    }
}

