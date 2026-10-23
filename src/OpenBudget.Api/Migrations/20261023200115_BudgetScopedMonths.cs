using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class BudgetScopedMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_CategoryGroups_BudgetMonth_GroupId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryGroups_Budgets_BudgetMonth",
                table: "CategoryGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CategoryGroups",
                table: "CategoryGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BudgetMonth_GroupId",
                table: "Categories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Budgets",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_BudgetId",
                table: "Budgets");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "CategoryGroups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"CategoryGroups\" AS groups SET \"BudgetId\" = budgets.\"BudgetId\" FROM \"Budgets\" AS budgets WHERE groups.\"BudgetMonth\" = budgets.\"Month\"");
            migrationBuilder.Sql("UPDATE \"Categories\" AS categories SET \"BudgetId\" = budgets.\"BudgetId\" FROM \"Budgets\" AS budgets WHERE categories.\"BudgetMonth\" = budgets.\"Month\"");

            migrationBuilder.AlterColumn<string>(
                name: "BudgetId",
                table: "CategoryGroups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "default",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BudgetId",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "default",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CategoryGroups",
                table: "CategoryGroups",
                columns: new[] { "BudgetId", "BudgetMonth", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                columns: new[] { "BudgetId", "BudgetMonth", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Budgets",
                table: "Budgets",
                columns: new[] { "BudgetId", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BudgetId_BudgetMonth_GroupId",
                table: "Categories",
                columns: new[] { "BudgetId", "BudgetMonth", "GroupId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_CategoryGroups_BudgetId_BudgetMonth_GroupId",
                table: "Categories",
                columns: new[] { "BudgetId", "BudgetMonth", "GroupId" },
                principalTable: "CategoryGroups",
                principalColumns: new[] { "BudgetId", "BudgetMonth", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryGroups_Budgets_BudgetId_BudgetMonth",
                table: "CategoryGroups",
                columns: new[] { "BudgetId", "BudgetMonth" },
                principalTable: "Budgets",
                principalColumns: new[] { "BudgetId", "Month" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_CategoryGroups_BudgetId_BudgetMonth_GroupId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryGroups_Budgets_BudgetId_BudgetMonth",
                table: "CategoryGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CategoryGroups",
                table: "CategoryGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BudgetId_BudgetMonth_GroupId",
                table: "Categories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Budgets",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "CategoryGroups");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Categories");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CategoryGroups",
                table: "CategoryGroups",
                columns: new[] { "BudgetMonth", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                columns: new[] { "BudgetMonth", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Budgets",
                table: "Budgets",
                column: "Month");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BudgetMonth_GroupId",
                table: "Categories",
                columns: new[] { "BudgetMonth", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BudgetId",
                table: "Budgets",
                column: "BudgetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_CategoryGroups_BudgetMonth_GroupId",
                table: "Categories",
                columns: new[] { "BudgetMonth", "GroupId" },
                principalTable: "CategoryGroups",
                principalColumns: new[] { "BudgetMonth", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryGroups_Budgets_BudgetMonth",
                table: "CategoryGroups",
                column: "BudgetMonth",
                principalTable: "Budgets",
                principalColumn: "Month",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
