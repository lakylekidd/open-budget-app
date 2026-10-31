using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve the previous display order (open before closed, then name, then ID) as each budget's initial order.
            migrationBuilder.Sql("""
                UPDATE "Accounts" AS a SET "SortOrder" = ordered.position
                FROM (SELECT "Id", (ROW_NUMBER() OVER (PARTITION BY "BudgetId" ORDER BY "IsClosed", "Name", "Id") - 1)::integer AS position
                      FROM "Accounts") AS ordered
                WHERE a."Id" = ordered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_BudgetId_SortOrder",
                table: "Accounts",
                columns: new[] { "BudgetId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_BudgetId_SortOrder",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Accounts");
        }
    }
}
