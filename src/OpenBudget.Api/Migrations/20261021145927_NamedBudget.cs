using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class NamedBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Budgets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateTable(
                name: "BudgetPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPlans", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BudgetPlans",
                columns: new[] { "Id", "Name", "Currency", "Version" },
                values: new object[] { "default", "My Budget", "EUR", 1L });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BudgetId",
                table: "Budgets",
                column: "BudgetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_BudgetPlans_BudgetId",
                table: "Budgets",
                column: "BudgetId",
                principalTable: "BudgetPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_BudgetPlans_BudgetId",
                table: "Budgets");

            migrationBuilder.DropTable(
                name: "BudgetPlans");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_BudgetId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Budgets");
        }
    }
}
