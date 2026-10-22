using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class PayeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryMode",
                table: "Payees",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "history");

            migrationBuilder.AddColumn<string>(
                name: "DefaultCategoryId",
                table: "Payees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Payees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesJson",
                table: "Payees",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryMode",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "DefaultCategoryId",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "RulesJson",
                table: "Payees");
        }
    }
}
