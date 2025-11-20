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

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.OwnershipGroups', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OwnershipGroups] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [Description] nvarchar(1000) NULL,
                        [Status] int NOT NULL,
                        [CreatedBy] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_OwnershipGroups] PRIMARY KEY ([Id])
                    );
                END");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.GroupFund', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[GroupFund] (
                        [Id] uniqueidentifier NOT NULL,
                        [GroupId] uniqueidentifier NOT NULL,
                        [TotalBalance] decimal(18, 2) NOT NULL,
                        [ReserveBalance] decimal(18, 2) NOT NULL,
                        [LastUpdated] datetime2 NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_GroupFund] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_GroupFund_OwnershipGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[OwnershipGroups] ([Id]) ON DELETE CASCADE
                    );
                END");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.FundTransaction', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[FundTransaction] (
                        [Id] uniqueidentifier NOT NULL,
                        [GroupId] uniqueidentifier NOT NULL,
                        [InitiatedBy] uniqueidentifier NOT NULL,
                        [Type] int NOT NULL,
                        [Amount] decimal(18, 2) NOT NULL,
                        [BalanceBefore] decimal(18, 2) NOT NULL,
                        [BalanceAfter] decimal(18, 2) NOT NULL,
                        [Description] nvarchar(500) NOT NULL,
                        [Status] int NOT NULL,
                        [ApprovedBy] uniqueidentifier NULL,
                        [TransactionDate] datetime2 NOT NULL,
                        [Reference] nvarchar(200) NULL,
                        [GroupFundId] uniqueidentifier NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_FundTransaction] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_FundTransaction_GroupFund_GroupFundId] FOREIGN KEY ([GroupFundId]) REFERENCES [dbo].[GroupFund] ([Id]),
                        CONSTRAINT [FK_FundTransaction_OwnershipGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[OwnershipGroups] ([Id]) ON DELETE CASCADE
                    );
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Vehicles_OwnershipGroupId' AND object_id = OBJECT_ID('dbo.Vehicles'))
                BEGIN
                    CREATE INDEX [IX_Vehicles_OwnershipGroupId] ON [dbo].[Vehicles] ([OwnershipGroupId]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RecurringBooking_GroupId' AND object_id = OBJECT_ID('dbo.RecurringBooking'))
                BEGIN
                    CREATE INDEX [IX_RecurringBooking_GroupId] ON [dbo].[RecurringBooking] ([GroupId]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FundTransaction_GroupFundId' AND object_id = OBJECT_ID('dbo.FundTransaction'))
                BEGIN
                    CREATE INDEX [IX_FundTransaction_GroupFundId] ON [dbo].[FundTransaction] ([GroupFundId]);
                END");
            
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FundTransaction_GroupId' AND object_id = OBJECT_ID('dbo.FundTransaction'))
                BEGIN
                    CREATE INDEX [IX_FundTransaction_GroupId] ON [dbo].[FundTransaction] ([GroupId]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GroupFund_GroupId' AND object_id = OBJECT_ID('dbo.GroupFund'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_GroupFund_GroupId] ON [dbo].[GroupFund] ([GroupId]);
                END");

            // NOTE: All FK constraints referencing GroupId are intentionally NOT created
            // because in microservices architecture, GroupId references Group Service (different DB)
            // See: scripts/fix-vehicle-groupid-fk.sql for more details
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS [dbo].[FundTransaction]");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS [dbo].[GroupFund]");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS [dbo].[OwnershipGroups]");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS [IX_Vehicles_OwnershipGroupId] ON [dbo].[Vehicles]");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS [IX_RecurringBooking_GroupId] ON [dbo].[RecurringBooking]");

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
