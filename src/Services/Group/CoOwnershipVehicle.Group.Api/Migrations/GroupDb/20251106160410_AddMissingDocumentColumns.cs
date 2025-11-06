using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoOwnershipVehicle.Group.Api.Migrations.GroupDb
{
    /// <inheritdoc />
    public partial class AddMissingDocumentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "DocumentSignatures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNotificationSent",
                table: "DocumentSignatures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "DocumentSignatures",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SigningMode",
                table: "DocumentSignatures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SigningToken",
                table: "DocumentSignatures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiresAt",
                table: "DocumentSignatures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVariablesJson",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingTemplate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    PreferredStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingTemplate_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingTemplate_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SharedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedWith = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Permissions = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccessCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FirstAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaxAccessCount = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Users_SharedBy",
                        column: x => x.SharedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTags_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentTags_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    TemplateContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PreviewImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ChangeDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LateReturnFee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LateDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CalculationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaivedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateReturnFee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LateReturnFee_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ServiceProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    OdometerReading = table.Column<int>(type: "int", nullable: true),
                    OdometerAtService = table.Column<int>(type: "int", nullable: true),
                    WorkPerformed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsUsed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsReplaced = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextServiceDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextServiceOdometer = table.Column<int>(type: "int", nullable: true),
                    ServiceProviderRating = table.Column<int>(type: "int", nullable: true),
                    ServiceProviderReview = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletionPercentage = table.Column<int>(type: "int", nullable: false),
                    PerformedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecord_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRecord_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBooking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastGeneratedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastGenerationRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pattern = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RecurrenceStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBooking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBooking_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBooking_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBooking_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedDocumentSearches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SearchCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    UsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDocumentSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedDocumentSearches_OwnershipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OwnershipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedDocumentSearches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignatureReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentSignatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderType = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    SentBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureReminders_DocumentSignatures_DocumentSignatureId",
                        column: x => x.DocumentSignatureId,
                        principalTable: "DocumentSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SigningCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalSigners = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningCertificates_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentShareAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentShareId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShareAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentShareAccesses_DocumentShares_DocumentShareId",
                        column: x => x.DocumentShareId,
                        principalTable: "DocumentShares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTagMappings",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaggedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTagMappings", x => new { x.DocumentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DocumentTagMappings_DocumentTags_TagId",
                        column: x => x.TagId,
                        principalTable: "DocumentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentTagMappings_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentTagMappings_Users_TaggedBy",
                        column: x => x.TaggedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_DueDate",
                table: "DocumentSignatures",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_SigningToken",
                table: "DocumentSignatures",
                column: "SigningToken",
                unique: true,
                filter: "[SigningToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_TokenExpiresAt",
                table: "DocumentSignatures",
                column: "TokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsDeleted",
                table: "Documents",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsDeleted_DeletedAt",
                table: "Documents",
                columns: new[] { "IsDeleted", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TemplateId",
                table: "Documents",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplate_UserId",
                table: "BookingTemplate",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTemplate_VehicleId",
                table: "BookingTemplate",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShareAccesses_AccessedAt",
                table: "DocumentShareAccesses",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShareAccesses_Action",
                table: "DocumentShareAccesses",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShareAccesses_DocumentShareId",
                table: "DocumentShareAccesses",
                column: "DocumentShareId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId",
                table: "DocumentShares",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_ExpiresAt",
                table: "DocumentShares",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_IsRevoked",
                table: "DocumentShares",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_SharedBy",
                table: "DocumentShares",
                column: "SharedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_ShareToken",
                table: "DocumentShares",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_ShareToken_IsRevoked_ExpiresAt",
                table: "DocumentShares",
                columns: new[] { "ShareToken", "IsRevoked", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMappings_DocumentId",
                table: "DocumentTagMappings",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMappings_TaggedAt",
                table: "DocumentTagMappings",
                column: "TaggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMappings_TaggedBy",
                table: "DocumentTagMappings",
                column: "TaggedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTagMappings_TagId",
                table: "DocumentTagMappings",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_CreatedBy",
                table: "DocumentTags",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_GroupId",
                table: "DocumentTags",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_Name",
                table: "DocumentTags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_Name_GroupId",
                table: "DocumentTags",
                columns: new[] { "Name", "GroupId" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Category",
                table: "DocumentTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Category_IsActive",
                table: "DocumentTemplates",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_CreatedBy",
                table: "DocumentTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_IsActive",
                table: "DocumentTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Name",
                table: "DocumentTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId",
                table: "DocumentVersions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_IsCurrent",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_VersionNumber",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_StorageKey",
                table: "DocumentVersions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_UploadedAt",
                table: "DocumentVersions",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_UploadedBy",
                table: "DocumentVersions",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_UserId",
                table: "LateReturnFee",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateReturnFee_VehicleId",
                table: "LateReturnFee",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecord_GroupId",
                table: "MaintenanceRecord",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecord_VehicleId",
                table: "MaintenanceRecord",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_GroupId",
                table: "RecurringBooking",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_UserId",
                table: "RecurringBooking",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBooking_VehicleId",
                table: "RecurringBooking",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDocumentSearches_GroupId",
                table: "SavedDocumentSearches",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDocumentSearches_UserId",
                table: "SavedDocumentSearches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDocumentSearches_UserId_IsDefault",
                table: "SavedDocumentSearches",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureReminders_DocumentSignatureId",
                table: "SignatureReminders",
                column: "DocumentSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureReminders_DocumentSignatureId_ReminderType",
                table: "SignatureReminders",
                columns: new[] { "DocumentSignatureId", "ReminderType" });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureReminders_SentAt",
                table: "SignatureReminders",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureReminders_Status",
                table: "SignatureReminders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SigningCertificates_CertificateId",
                table: "SigningCertificates",
                column: "CertificateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SigningCertificates_DocumentHash_CertificateId",
                table: "SigningCertificates",
                columns: new[] { "DocumentHash", "CertificateId" });

            migrationBuilder.CreateIndex(
                name: "IX_SigningCertificates_DocumentId",
                table: "SigningCertificates",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SigningCertificates_GeneratedAt",
                table: "SigningCertificates",
                column: "GeneratedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentTemplates_TemplateId",
                table: "Documents",
                column: "TemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentTemplates_TemplateId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "BookingTemplate");

            migrationBuilder.DropTable(
                name: "DocumentShareAccesses");

            migrationBuilder.DropTable(
                name: "DocumentTagMappings");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "LateReturnFee");

            migrationBuilder.DropTable(
                name: "MaintenanceRecord");

            migrationBuilder.DropTable(
                name: "RecurringBooking");

            migrationBuilder.DropTable(
                name: "SavedDocumentSearches");

            migrationBuilder.DropTable(
                name: "SignatureReminders");

            migrationBuilder.DropTable(
                name: "SigningCertificates");

            migrationBuilder.DropTable(
                name: "DocumentShares");

            migrationBuilder.DropTable(
                name: "DocumentTags");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_DueDate",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_SigningToken",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSignatures_TokenExpiresAt",
                table: "DocumentSignatures");

            migrationBuilder.DropIndex(
                name: "IX_Documents_IsDeleted",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_IsDeleted_DeletedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TemplateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "IsNotificationSent",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "SigningMode",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "SigningToken",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "TokenExpiresAt",
                table: "DocumentSignatures");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TemplateVariablesJson",
                table: "Documents");
        }
    }
}
