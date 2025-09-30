using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Analytics.Api.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    TotalDistance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalBookings = table.Column<int>(type: "int", nullable: false),
                    TotalUsageHours = table.Column<int>(type: "int", nullable: false),
                    ActiveUsers = table.Column<int>(type: "int", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageCostPerHour = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageCostPerKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UtilizationRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    MaintenanceEfficiency = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    UserSatisfactionScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckInPhoto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInPhoto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSignature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSignature", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_GroupId",
                table: "AnalyticsSnapshots",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_Period",
                table: "AnalyticsSnapshots",
                column: "Period");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_SnapshotDate",
                table: "AnalyticsSnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_VehicleId",
                table: "AnalyticsSnapshots",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "CheckInPhoto");

            migrationBuilder.DropTable(
                name: "DocumentSignature");
        }
    }
}
