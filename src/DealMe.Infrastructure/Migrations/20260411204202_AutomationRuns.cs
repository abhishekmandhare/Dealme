using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutomationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Task = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItemsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    PointsAfter = table.Column<int>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    LogOutput = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoRun_Provider_Started",
                table: "AutomationRuns",
                columns: new[] { "Provider", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRuns");
        }
    }
}
