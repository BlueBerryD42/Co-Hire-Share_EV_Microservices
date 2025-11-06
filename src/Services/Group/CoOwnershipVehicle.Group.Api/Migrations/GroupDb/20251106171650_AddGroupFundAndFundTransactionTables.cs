using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Group.Api.Migrations.GroupDb
{
    /// <inheritdoc />
    public partial class AddGroupFundAndFundTransactionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupFunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReserveBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFunds_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundTransactions",
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
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroupFundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundTransactions_GroupFunds_GroupFundId",
                        column: x => x.GroupFundId,
                        principalTable: "GroupFunds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FundTransactions_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FundTransactions_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransactions_Users_InitiatedBy",
                        column: x => x.InitiatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_ApprovedBy",
                table: "FundTransactions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_GroupFundId",
                table: "FundTransactions",
                column: "GroupFundId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_GroupId",
                table: "FundTransactions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_GroupId_Status",
                table: "FundTransactions",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_InitiatedBy",
                table: "FundTransactions",
                column: "InitiatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_Status",
                table: "FundTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_TransactionDate",
                table: "FundTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_Type",
                table: "FundTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFunds_GroupId",
                table: "GroupFunds",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupFunds_LastUpdated",
                table: "GroupFunds",
                column: "LastUpdated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundTransactions");

            migrationBuilder.DropTable(
                name: "GroupFunds");
        }
    }
}
