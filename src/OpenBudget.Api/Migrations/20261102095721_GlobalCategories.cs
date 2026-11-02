using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <summary>
    /// Makes category groups and categories budget-wide (the same for every month) and keeps money per month.
    /// Scaffolded with <c>dotnet ef migrations add</c>; Up/Down are hand-written because the scaffold would drop the
    /// per-month money columns. Rules (docs/global-categories.md#migration):
    /// <list type="bullet">
    /// <item>Every legacy per-month category row becomes one <c>CategoryMonths</c> row with identical money, snooze and
    /// legacy target values (verified inside the migration; any mismatch aborts and rolls it back).</item>
    /// <item>Definitions are deduplicated by (budget, ID): the row from the most recent month that has the group or
    /// category wins (name, group, icon, order, archived). Sort orders are then renumbered 0..n per budget/group,
    /// keeping that order (ties: more recent month first, then ID).</item>
    /// <item>No category ID changes, so transactions, splits, schedules, targets, payees, loans, Wishlist and credit card
    /// payment categories keep their references.</item>
    /// <item>The legacy tables are kept, renamed, as a read-only pre-migration copy (<c>LegacyMonthCategoryGroups</c>,
    /// <c>LegacyMonthCategories</c>) for audit and manual recovery; a later release drops them.</item>
    /// </list>
    /// EF runs the migration in one transaction; the data statements are also safe to run again (ON CONFLICT DO NOTHING).
    /// </summary>
    public partial class GlobalCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "CategoryGroups", newName: "LegacyMonthCategoryGroups");
            migrationBuilder.RenameTable(name: "Categories", newName: "LegacyMonthCategories");

            migrationBuilder.AddColumn<bool>(
                name: "CategoriesInitialized",
                table: "BudgetPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BudgetCategoryGroups",
                columns: table => new
                {
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategoryGroups", x => new { x.BudgetId, x.Id });
                    table.ForeignKey(
                        name: "FK_BudgetCategoryGroups_BudgetPlans_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetCategories",
                columns: table => new
                {
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Icon = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategories", x => new { x.BudgetId, x.Id });
                    table.ForeignKey(
                        name: "FK_BudgetCategories_BudgetCategoryGroups_BudgetId_GroupId",
                        columns: x => new { x.BudgetId, x.GroupId },
                        principalTable: "BudgetCategoryGroups",
                        principalColumns: new[] { "BudgetId", "Id" });
                    table.ForeignKey(
                        name: "FK_BudgetCategories_BudgetPlans_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryMonths",
                columns: table => new
                {
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BudgetMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OpeningAvailable = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Assigned = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LegacyActivity = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Activity = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreditOverspending = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Target = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Snoozed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryMonths", x => new { x.BudgetId, x.BudgetMonth, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_CategoryMonths_BudgetCategories_BudgetId_CategoryId",
                        columns: x => new { x.BudgetId, x.CategoryId },
                        principalTable: "BudgetCategories",
                        principalColumns: new[] { "BudgetId", "Id" });
                    table.ForeignKey(
                        name: "FK_CategoryMonths_Budgets_BudgetId_BudgetMonth",
                        columns: x => new { x.BudgetId, x.BudgetMonth },
                        principalTable: "Budgets",
                        principalColumns: new[] { "BudgetId", "Month" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_BudgetId_GroupId",
                table: "BudgetCategories",
                columns: new[] { "BudgetId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMonths_BudgetId_CategoryId",
                table: "CategoryMonths",
                columns: new[] { "BudgetId", "CategoryId" });

            // Group definitions: the most recent month's row per (budget, group); order renumbered per budget.
            migrationBuilder.Sql("""
                INSERT INTO "BudgetCategoryGroups" ("BudgetId", "Id", "Name", "SortOrder", "IsArchived")
                SELECT latest."BudgetId", latest."Id", latest."Name",
                       (ROW_NUMBER() OVER (PARTITION BY latest."BudgetId" ORDER BY latest."SortOrder", latest."BudgetMonth" DESC, latest."Id") - 1)::integer,
                       latest."IsArchived"
                FROM (SELECT DISTINCT ON (g."BudgetId", g."Id") g."BudgetId", g."Id", g."BudgetMonth", g."Name", g."SortOrder", g."IsArchived"
                      FROM "LegacyMonthCategoryGroups" AS g
                      ORDER BY g."BudgetId", g."Id", g."BudgetMonth" DESC) AS latest
                ON CONFLICT ("BudgetId", "Id") DO NOTHING;
                """);

            // Category definitions: the most recent month's row per (budget, category), including its group; order
            // renumbered per budget and group.
            migrationBuilder.Sql("""
                INSERT INTO "BudgetCategories" ("BudgetId", "Id", "GroupId", "Name", "Icon", "SortOrder", "IsArchived")
                SELECT latest."BudgetId", latest."Id", latest."GroupId", latest."Name", latest."Icon",
                       (ROW_NUMBER() OVER (PARTITION BY latest."BudgetId", latest."GroupId" ORDER BY latest."SortOrder", latest."BudgetMonth" DESC, latest."Id") - 1)::integer,
                       latest."IsArchived"
                FROM (SELECT DISTINCT ON (c."BudgetId", c."Id") c."BudgetId", c."Id", c."BudgetMonth", c."GroupId", c."Name", c."Icon", c."SortOrder", c."IsArchived"
                      FROM "LegacyMonthCategories" AS c
                      ORDER BY c."BudgetId", c."Id", c."BudgetMonth" DESC) AS latest
                ON CONFLICT ("BudgetId", "Id") DO NOTHING;
                """);

            // Month state: every legacy row, unchanged.
            migrationBuilder.Sql("""
                INSERT INTO "CategoryMonths" ("BudgetId", "BudgetMonth", "CategoryId", "OpeningAvailable", "Assigned", "LegacyActivity", "Activity", "CreditOverspending", "Target", "Snoozed")
                SELECT c."BudgetId", c."BudgetMonth", c."Id", c."OpeningAvailable", c."Assigned", c."LegacyActivity", c."Activity", c."CreditOverspending", c."Target", c."Snoozed"
                FROM "LegacyMonthCategories" AS c
                ON CONFLICT ("BudgetId", "BudgetMonth", "CategoryId") DO NOTHING;
                """);

            // A budget that already has months or categories never receives the starter template again.
            migrationBuilder.Sql("""
                UPDATE "BudgetPlans" AS p SET "CategoriesInitialized" = TRUE
                WHERE EXISTS (SELECT 1 FROM "Budgets" AS b WHERE b."BudgetId" = p."Id")
                   OR EXISTS (SELECT 1 FROM "BudgetCategoryGroups" AS g WHERE g."BudgetId" = p."Id");
                """);

            // Abort (and roll back the whole migration) unless every legacy month row survived with identical values and
            // every legacy group/category has a definition.
            migrationBuilder.Sql("""
                DO $verify$
                BEGIN
                    IF EXISTS (
                        SELECT c."BudgetId", c."BudgetMonth", c."Id", c."OpeningAvailable", c."Assigned", c."LegacyActivity", c."Activity", c."CreditOverspending", c."Target", c."Snoozed"
                        FROM "LegacyMonthCategories" AS c
                        EXCEPT
                        SELECT m."BudgetId", m."BudgetMonth", m."CategoryId", m."OpeningAvailable", m."Assigned", m."LegacyActivity", m."Activity", m."CreditOverspending", m."Target", m."Snoozed"
                        FROM "CategoryMonths" AS m) THEN
                        RAISE EXCEPTION 'GlobalCategories: per-month category state was not preserved';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "LegacyMonthCategories" AS c
                               WHERE NOT EXISTS (SELECT 1 FROM "BudgetCategories" AS d WHERE d."BudgetId" = c."BudgetId" AND d."Id" = c."Id"))
                       OR EXISTS (SELECT 1 FROM "LegacyMonthCategoryGroups" AS g
                               WHERE NOT EXISTS (SELECT 1 FROM "BudgetCategoryGroups" AS d WHERE d."BudgetId" = g."BudgetId" AND d."Id" = g."Id")) THEN
                        RAISE EXCEPTION 'GlobalCategories: a category or group definition is missing';
                    END IF;
                END
                $verify$;
                """);
        }

        /// <summary>
        /// Rebuilds the per-month tables from the current global data: every initialized month gets every group and
        /// category with that month's state (zero where a month had no state row). Category deletions done after the
        /// upgrade are not undone; their history was already reassigned. Take a backup before running Down too.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "LegacyMonthCategories";
                DELETE FROM "LegacyMonthCategoryGroups";
                INSERT INTO "LegacyMonthCategoryGroups" ("BudgetId", "BudgetMonth", "Id", "Name", "SortOrder", "IsArchived")
                SELECT b."BudgetId", b."Month", g."Id", g."Name", g."SortOrder", g."IsArchived"
                FROM "Budgets" AS b JOIN "BudgetCategoryGroups" AS g ON g."BudgetId" = b."BudgetId";
                INSERT INTO "LegacyMonthCategories" ("BudgetId", "BudgetMonth", "Id", "GroupId", "Name", "OpeningAvailable", "Assigned", "LegacyActivity", "Activity",
                    "CreditOverspending", "Target", "Icon", "Snoozed", "SortOrder", "IsArchived")
                SELECT b."BudgetId", b."Month", c."Id", c."GroupId", c."Name", COALESCE(m."OpeningAvailable", 0), COALESCE(m."Assigned", 0), COALESCE(m."LegacyActivity", 0),
                    COALESCE(m."Activity", 0), COALESCE(m."CreditOverspending", 0), COALESCE(m."Target", 0), c."Icon", COALESCE(m."Snoozed", FALSE), c."SortOrder", c."IsArchived"
                FROM "Budgets" AS b
                JOIN "BudgetCategories" AS c ON c."BudgetId" = b."BudgetId"
                LEFT JOIN "CategoryMonths" AS m ON m."BudgetId" = b."BudgetId" AND m."BudgetMonth" = b."Month" AND m."CategoryId" = c."Id";
                """);

            migrationBuilder.DropTable(name: "CategoryMonths");
            migrationBuilder.DropTable(name: "BudgetCategories");
            migrationBuilder.DropTable(name: "BudgetCategoryGroups");
            migrationBuilder.DropColumn(name: "CategoriesInitialized", table: "BudgetPlans");
            migrationBuilder.RenameTable(name: "LegacyMonthCategories", newName: "Categories");
            migrationBuilder.RenameTable(name: "LegacyMonthCategoryGroups", newName: "CategoryGroups");
        }
    }
}
