using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealMe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileSearchCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MobileSearchCount",
                table: "AutomationProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MobileSearchCount",
                table: "AutomationProviderConfigs");
        }
    }
}
