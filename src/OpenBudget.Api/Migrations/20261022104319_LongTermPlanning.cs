using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class LongTermPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LongTermPlanningCategories",
                columns: table => new
                {
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlannedBudgetOverride = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    PlannedExpenses = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LongTermPlanningCategories", x => new { x.Month, x.CategoryId });
                });

            migrationBuilder.CreateTable(
                name: "LongTermPlanningIncomes",
                columns: table => new
                {
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ExpectedIncome = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LongTermPlanningIncomes", x => x.Month);
                });

            migrationBuilder.CreateTable(
                name: "LongTermPlanningStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LongTermPlanningStates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LongTermPlanningStates",
                columns: new[] { "Id", "Version" },
                values: new object[] { "default", 1L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LongTermPlanningCategories");

            migrationBuilder.DropTable(
                name: "LongTermPlanningIncomes");

            migrationBuilder.DropTable(
                name: "LongTermPlanningStates");
        }
    }
}
