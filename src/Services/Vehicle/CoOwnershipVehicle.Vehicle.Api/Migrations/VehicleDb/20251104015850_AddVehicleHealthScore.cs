using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class AddVehicleHealthScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleHealthScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaintenanceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OdometerAgeScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DamageScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ServiceFrequencyScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VehicleAgeScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    InspectionScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OdometerAtCalculation = table.Column<int>(type: "int", nullable: false),
                    OverdueMaintenanceCount = table.Column<int>(type: "int", nullable: false),
                    DamageReportCount = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleHealthScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleHealthScores_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleHealthScores_CalculatedAt",
                table: "VehicleHealthScores",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleHealthScores_VehicleId",
                table: "VehicleHealthScores",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleHealthScores_VehicleId_CalculatedAt",
                table: "VehicleHealthScores",
                columns: new[] { "VehicleId", "CalculatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleHealthScores");
        }
    }
}
