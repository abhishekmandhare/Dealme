using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PriceWatchSearchQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Url",
                table: "PriceWatches");

            migrationBuilder.AddColumn<string>(
                name: "CheapestRetailer",
                table: "PriceWatches",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheapestUrl",
                table: "PriceWatches",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchQuery",
                table: "PriceWatches",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheapestRetailer",
                table: "PriceWatches");

            migrationBuilder.DropColumn(
                name: "CheapestUrl",
                table: "PriceWatches");

            migrationBuilder.DropColumn(
                name: "SearchQuery",
                table: "PriceWatches");

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "PriceWatches",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");
        }
    }
}
