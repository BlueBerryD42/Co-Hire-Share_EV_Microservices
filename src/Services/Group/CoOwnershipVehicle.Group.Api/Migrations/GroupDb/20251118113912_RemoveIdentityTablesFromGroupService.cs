using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Group.Api.Migrations.GroupDb
{
    /// <inheritdoc />
    public partial class RemoveIdentityTablesFromGroupService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingTemplate_Users_UserId",
                table: "BookingTemplate");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingTemplate_Vehicles_VehicleId",
                table: "BookingTemplate");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDownloads_Users_UserId",
                table: "DocumentDownloads");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentShares_Users_SharedBy",
                table: "DocumentShares");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTagMappings_Users_TaggedBy",
                table: "DocumentTagMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTags_Users_CreatedBy",
                table: "DocumentTags");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTemplates_Users_CreatedBy",
                table: "DocumentTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_Users_UploadedBy",
                table: "DocumentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_FundTransactions_Users_ApprovedBy",
                table: "FundTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FundTransactions_Users_InitiatedBy",
                table: "FundTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMembers_Users_UserId",
                table: "GroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_LateReturnFee_Users_UserId",
                table: "LateReturnFee");

            migrationBuilder.DropForeignKey(
                name: "FK_LateReturnFee_Vehicles_VehicleId",
                table: "LateReturnFee");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecord_Vehicles_VehicleId",
                table: "MaintenanceRecord");

            migrationBuilder.DropForeignKey(
                name: "FK_OwnershipGroups_Users_CreatedBy",
                table: "OwnershipGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Users_CreatedBy",
                table: "Proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBooking_Users_UserId",
                table: "RecurringBooking");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringBooking_Vehicles_VehicleId",
                table: "RecurringBooking");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedDocumentSearches_Users_UserId",
                table: "SavedDocumentSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Users_VoterId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_RecurringBooking_UserId",
                table: "RecurringBooking");

            migrationBuilder.DropIndex(
                name: "IX_RecurringBooking_VehicleId",
                table: "RecurringBooking");

            migrationBuilder.DropIndex(
                name: "IX_OwnershipGroups_CreatedBy",
                table: "OwnershipGroups");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecord_VehicleId",
                table: "MaintenanceRecord");

            migrationBuilder.DropIndex(
                name: "IX_LateReturnFee_UserId",
                table: "LateReturnFee");

            migrationBuilder.DropIndex(
                name: "IX_LateReturnFee_VehicleId",
                table: "LateReturnFee");

            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_FundTransactions_ApprovedBy",
                table: "FundTransactions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_UploadedBy",
                table: "DocumentVersions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTemplates_CreatedBy",
                table: "DocumentTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTags_CreatedBy",
                table: "DocumentTags");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTagMappings_TaggedBy",
                table: "DocumentTagMappings");

            migrationBuilder.DropIndex(
                name: "IX_DocumentShares_SharedBy",
                table: "DocumentShares");

            migrationBuilder.DropIndex(
                name: "IX_BookingTemplate_UserId",
                table: "BookingTemplate");

            migrationBuilder.DropIndex(
                name: "IX_BookingTemplate_VehicleId",
                table: "BookingTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KycStatus = table.Column<int>(type: "int", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Odometer = table.Column<int>(type: "int", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Vin = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_UserId",
                table: "RecurringBooking",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_VehicleId",
                table: "RecurringBooking",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipGroups_CreatedBy",
                table: "OwnershipGroups",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecord_VehicleId",
                table: "MaintenanceRecord",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_UserId",
                table: "LateReturnFee",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_ApprovedBy",
                table: "FundTransactions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_UploadedBy",
                table: "DocumentVersions",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_CreatedBy",
                table: "DocumentTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_CreatedBy",
                table: "DocumentTags",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMappings_TaggedBy",
                table: "DocumentTagMappings",
                column: "TaggedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_SharedBy",
                table: "DocumentShares",
                column: "SharedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplate_UserId",
                table: "BookingTemplate",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplate_VehicleId",
                table: "BookingTemplate",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_GroupId",
                table: "Vehicles",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PlateNumber",
                table: "Vehicles",
                column: "PlateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Vin",
                table: "Vehicles",
                column: "Vin",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingTemplate_Users_UserId",
                table: "BookingTemplate",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingTemplate_Vehicles_VehicleId",
                table: "BookingTemplate",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDownloads_Users_UserId",
                table: "DocumentDownloads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentShares_Users_SharedBy",
                table: "DocumentShares",
                column: "SharedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures",
                column: "SignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTagMappings_Users_TaggedBy",
                table: "DocumentTagMappings",
                column: "TaggedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTags_Users_CreatedBy",
                table: "DocumentTags",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTemplates_Users_CreatedBy",
                table: "DocumentTemplates",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_Users_UploadedBy",
                table: "DocumentVersions",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FundTransactions_Users_ApprovedBy",
                table: "FundTransactions",
                column: "ApprovedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FundTransactions_Users_InitiatedBy",
                table: "FundTransactions",
                column: "InitiatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMembers_Users_UserId",
                table: "GroupMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LateReturnFee_Users_UserId",
                table: "LateReturnFee",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LateReturnFee_Vehicles_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecord_Vehicles_VehicleId",
                table: "MaintenanceRecord",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OwnershipGroups_Users_CreatedBy",
                table: "OwnershipGroups",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Users_CreatedBy",
                table: "Proposals",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBooking_Users_UserId",
                table: "RecurringBooking",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringBooking_Vehicles_VehicleId",
                table: "RecurringBooking",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedDocumentSearches_Users_UserId",
                table: "SavedDocumentSearches",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Users_VoterId",
                table: "Votes",
                column: "VoterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
