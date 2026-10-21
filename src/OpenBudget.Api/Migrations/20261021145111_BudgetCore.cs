using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class BudgetCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Available",
                table: "Categories",
                newName: "OpeningAvailable");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CategoryGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FundsAdded",
                table: "Budgets",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningReadyToAssign",
                table: "Budgets",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Budgets",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql("""
                UPDATE "Categories"
                SET "OpeningAvailable" = "OpeningAvailable" - "Assigned" - "Activity";

                UPDATE "Budgets" AS budget
                SET "FundsAdded" = COALESCE((
                    SELECT SUM(category."Assigned")
                    FROM "Categories" AS category
                    WHERE category."BudgetMonth" = budget."Month"
                ), 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Categories"
                SET "OpeningAvailable" = "OpeningAvailable" + "Assigned" + "Activity";
                """);

            migrationBuilder.DropColumn(name: "IsArchived", table: "CategoryGroups");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Categories");
            migrationBuilder.DropColumn(name: "FundsAdded", table: "Budgets");
            migrationBuilder.DropColumn(name: "OpeningReadyToAssign", table: "Budgets");
            migrationBuilder.DropColumn(name: "Version", table: "Budgets");

            migrationBuilder.RenameColumn(
                name: "OpeningAvailable",
                table: "Categories",
                newName: "Available");
        }
    }
}
