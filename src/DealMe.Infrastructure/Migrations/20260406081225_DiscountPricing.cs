using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiscountPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "Opportunities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalValue",
                table: "Opportunities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "OriginalValue",
                table: "Opportunities");
        }
    }
}
