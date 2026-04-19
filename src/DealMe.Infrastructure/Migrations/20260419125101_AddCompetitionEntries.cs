using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SearchCount",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 33);

            migrationBuilder.AlterColumn<int>(
                name: "MinDelayMs",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 8000);

            migrationBuilder.AlterColumn<int>(
                name: "MaxDelayMs",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 20000);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.CreateTable(
                name: "CompetitionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CompetitionUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EmailUsed = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AttemptedAt = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompEntry_OpportunityId",
                table: "CompetitionEntries",
                column: "OpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompEntry_Status_Created",
                table: "CompetitionEntries",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionEntries");

            migrationBuilder.AlterColumn<int>(
                name: "SearchCount",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 33,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "MinDelayMs",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 8000,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "MaxDelayMs",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20000,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");
        }
    }
}
