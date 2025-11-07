using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Group.Api.Migrations.GroupDb
{
    /// <inheritdoc />
    public partial class AddDocumentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignature_Users_SignerId",
                table: "DocumentSignature");

            migrationBuilder.DropTable(
                name: "CheckInPhoto");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DocumentSignature",
                table: "DocumentSignature");

            migrationBuilder.RenameTable(
                name: "DocumentSignature",
                newName: "DocumentSignatures");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentSignature_SignerId",
                table: "DocumentSignatures",
                newName: "IX_DocumentSignatures_SignerId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignedAt",
                table: "DocumentSignatures",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "SignatureReference",
                table: "DocumentSignatures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "SignatureMetadata",
                table: "DocumentSignatures",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureOrder",
                table: "DocumentSignatures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DocumentSignatures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentSignatures",
                table: "DocumentSignatures",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignatureStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsVirusScanned = table.Column<bool>(type: "bit", nullable: false),
                    VirusScanPassed = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_DocumentId",
                table: "DocumentSignatures",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_DocumentId_SignatureOrder",
                table: "DocumentSignatures",
                columns: new[] { "DocumentId", "SignatureOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_DocumentId_SignerId",
                table: "DocumentSignatures",
                columns: new[] { "DocumentId", "SignerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedAt",
                table: "Documents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GroupId",
                table: "Documents",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GroupId_FileHash",
                table: "Documents",
                columns: new[] { "GroupId", "FileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StorageKey",
                table: "Documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignatures_Documents_DocumentId",
                table: "DocumentSignatures",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures",
                column: "SignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignatures_Documents_DocumentId",
                table: "DocumentSignatures");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DocumentSignatures",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_DocumentId",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_DocumentId_SignatureOrder",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_DocumentId_SignerId",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "SignatureMetadata",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "SignatureOrder",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DocumentSignatures");

            migrationBuilder.RenameTable(
                name: "DocumentSignatures",
                newName: "DocumentSignature");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentSignatures_SignerId",
                table: "DocumentSignature",
                newName: "IX_DocumentSignature_SignerId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignedAt",
                table: "DocumentSignature",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignatureReference",
                table: "DocumentSignature",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DocumentSignature",
                table: "DocumentSignature",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "CheckInPhoto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInPhoto", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignature_Users_SignerId",
                table: "DocumentSignature",
                column: "SignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
