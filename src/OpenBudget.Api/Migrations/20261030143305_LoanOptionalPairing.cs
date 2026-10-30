using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class LoanOptionalPairing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DevicePairedCategoryId",
                table: "LoanTerms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeparateDeviceCategory",
                table: "LoanTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_LoanTerms_BudgetId_DevicePairedCategoryId",
                table: "LoanTerms",
                columns: new[] { "BudgetId", "DevicePairedCategoryId" },
                unique: true,
                filter: "\"DevicePairedCategoryId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoanTerms_BudgetId_DevicePairedCategoryId",
                table: "LoanTerms");

            migrationBuilder.DropColumn(
                name: "DevicePairedCategoryId",
                table: "LoanTerms");

            migrationBuilder.DropColumn(
                name: "SeparateDeviceCategory",
                table: "LoanTerms");
        }
    }
}
