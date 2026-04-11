using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AppriseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<byte>(type: "INTEGER", nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<decimal>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Confidence = table.Column<byte>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    Metadata = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TargetPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CdioWatchId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceWatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MinDealValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    QuietHoursStart = table.Column<string>(type: "TEXT", nullable: true),
                    QuietHoursEnd = table.Column<string>(type: "TEXT", nullable: true),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EnabledCategories = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    InterestTags = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Event_Aggregate",
                table: "DomainEvents",
                columns: new[] { "AggregateType", "AggregateId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Event_Correlation",
                table: "DomainEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Opp_ExpiresAt",
                table: "Opportunities",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Opp_Source_SourceId",
                table: "Opportunities",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Opp_Status_Created",
                table: "Opportunities",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PW_Active",
                table: "PriceWatches",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainEvents");

            migrationBuilder.DropTable(
                name: "NotificationChannels");

            migrationBuilder.DropTable(
                name: "Opportunities");

            migrationBuilder.DropTable(
                name: "PriceWatches");

            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
