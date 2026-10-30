using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class CreditAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditOverspending",
                table: "Categories",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditOverspending",
                table: "Categories");
        }
    }
}
