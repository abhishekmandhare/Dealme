using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationProviderConfigs",
                columns: table => new
                {
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccountLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CronSchedule = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "0 21 * * *"),
                    SearchCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 33),
                    MinDelayMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 8000),
                    MaxDelayMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 20000),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationProviderConfigs", x => x.ProviderId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationProviderConfigs");
        }
    }
}
