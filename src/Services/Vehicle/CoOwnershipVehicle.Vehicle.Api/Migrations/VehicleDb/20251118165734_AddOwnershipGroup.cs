using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Vehicle.Api.Migrations.VehicleDb
{
    /// <inheritdoc />
    public partial class AddOwnershipGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnershipGroupId",
                table: "Vehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "CheckInPhoto",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "OwnershipGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnershipGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupFund",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReserveBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFund_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroupFundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundTransaction_GroupFund_GroupFundId",
                        column: x => x.GroupFundId,
                        principalTable: "GroupFund",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FundTransaction_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnershipGroupId",
                table: "Vehicles",
                column: "OwnershipGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_GroupId",
                table: "RecurringBooking",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_GroupFundId",
                table: "FundTransaction",
                column: "GroupFundId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_GroupId",
                table: "FundTransaction",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFund_GroupId",
                table: "GroupFund",
                column: "GroupId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBooking_OwnershipGroups_GroupId",
                table: "RecurringBooking",
                column: "GroupId",
                principalTable: "OwnershipGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // NOTE: FK constraints for Vehicles.GroupId and Vehicles.OwnershipGroupId are intentionally
            // NOT created because in microservices architecture, GroupId references Group Service (different DB)
            // See: scripts/fix-vehicle-groupid-fk.sql for more details

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Vehicles_OwnershipGroups_GroupId",
            //     table: "Vehicles",
            //     column: "GroupId",
            //     principalTable: "OwnershipGroups",
            //     principalColumn: "Id");

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Vehicles_OwnershipGroups_OwnershipGroupId",
            //     table: "Vehicles",
            //     column: "OwnershipGroupId",
            //     principalTable: "OwnershipGroups",
            //     principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBooking_OwnershipGroups_GroupId",
                table: "RecurringBooking");

            // FK constraints were not created in Up(), so don't drop them in Down()
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Vehicles_OwnershipGroups_GroupId",
            //     table: "Vehicles");

            // migrationBuilder.DropForeignKey(
            //     name: "FK_Vehicles_OwnershipGroups_OwnershipGroupId",
            //     table: "Vehicles");

            migrationBuilder.DropTable(
                name: "FundTransaction");

            migrationBuilder.DropTable(
                name: "GroupFund");

            migrationBuilder.DropTable(
                name: "OwnershipGroups");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_OwnershipGroupId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_RecurringBooking_GroupId",
                table: "RecurringBooking");

            migrationBuilder.DropColumn(
                name: "OwnershipGroupId",
                table: "Vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "CheckInPhoto",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);
        }
    }
}
