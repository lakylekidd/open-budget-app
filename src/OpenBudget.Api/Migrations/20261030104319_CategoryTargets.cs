using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class CategoryTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryTargets",
                columns: table => new
                {
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Behavior = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Weekday = table.Column<int>(type: "integer", nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RepeatEvery = table.Column<int>(type: "integer", nullable: true),
                    RepeatUnit = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    StartMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTargets", x => new { x.BudgetId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_CategoryTargets_BudgetPlans_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryTargets_Id",
                table: "CategoryTargets",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryTargets");
        }
    }
}
