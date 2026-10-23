using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class BudgetFinancialIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Schedules_AccountId_NextDate",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleOccurrences_ScheduleId_Sequence",
                table: "ScheduleOccurrences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RegisterViews",
                table: "RegisterViews");

            migrationBuilder.DropIndex(
                name: "IX_Payees_NormalizedName",
                table: "Payees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningStates",
                table: "LongTermPlanningStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningIncomes",
                table: "LongTermPlanningIncomes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningCategories",
                table: "LongTermPlanningCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LedgerStates",
                table: "LedgerStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagDefinitions",
                table: "FlagDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Schedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "ScheduleOccurrences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "RegisterViews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Reconciliations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Payees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "LongTermPlanningStates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "LongTermPlanningIncomes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "LongTermPlanningCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LedgerStates",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "LedgerStates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "LedgerCommands",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "FlagDefinitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                table: "Accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Accounts" SET "BudgetId" = 'default';
                UPDATE "Payees" SET "BudgetId" = 'default';
                UPDATE "Transactions" SET "BudgetId" = 'default';
                UPDATE "LedgerStates" SET "BudgetId" = 'default';
                UPDATE "LedgerCommands" SET "BudgetId" = 'default';
                UPDATE "Reconciliations" SET "BudgetId" = 'default';
                UPDATE "RegisterViews" SET "BudgetId" = 'default';
                UPDATE "FlagDefinitions" SET "BudgetId" = 'default';
                UPDATE "Schedules" SET "BudgetId" = 'default';
                UPDATE "ScheduleOccurrences" SET "BudgetId" = 'default';
                UPDATE "LongTermPlanningStates" SET "BudgetId" = 'default';
                UPDATE "LongTermPlanningIncomes" SET "BudgetId" = 'default';
                UPDATE "LongTermPlanningCategories" SET "BudgetId" = 'default';
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RegisterViews",
                table: "RegisterViews",
                columns: new[] { "BudgetId", "Scope" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningStates",
                table: "LongTermPlanningStates",
                columns: new[] { "BudgetId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningIncomes",
                table: "LongTermPlanningIncomes",
                columns: new[] { "BudgetId", "Month" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningCategories",
                table: "LongTermPlanningCategories",
                columns: new[] { "BudgetId", "Month", "CategoryId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LedgerStates",
                table: "LedgerStates",
                columns: new[] { "BudgetId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagDefinitions",
                table: "FlagDefinitions",
                columns: new[] { "BudgetId", "Color" });


            migrationBuilder.CreateIndex(
                name: "IX_Schedules_AccountId",
                table: "Schedules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_BudgetId_AccountId_NextDate",
                table: "Schedules",
                columns: new[] { "BudgetId", "AccountId", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOccurrences_BudgetId_ScheduleId_Sequence",
                table: "ScheduleOccurrences",
                columns: new[] { "BudgetId", "ScheduleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOccurrences_ScheduleId",
                table: "ScheduleOccurrences",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Payees_BudgetId_NormalizedName",
                table: "Payees",
                columns: new[] { "BudgetId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Accounts" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "Payees" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "Transactions" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "LedgerStates" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "LedgerCommands" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "Reconciliations" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "RegisterViews" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "FlagDefinitions" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "Schedules" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "ScheduleOccurrences" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "LongTermPlanningStates" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "LongTermPlanningIncomes" WHERE "BudgetId" <> 'default')
                       OR EXISTS (SELECT 1 FROM "LongTermPlanningCategories" WHERE "BudgetId" <> 'default') THEN
                        RAISE EXCEPTION 'Cannot remove budget isolation while non-default financial data exists.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Schedules_AccountId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_BudgetId_AccountId_NextDate",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleOccurrences_BudgetId_ScheduleId_Sequence",
                table: "ScheduleOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleOccurrences_ScheduleId",
                table: "ScheduleOccurrences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RegisterViews",
                table: "RegisterViews");

            migrationBuilder.DropIndex(
                name: "IX_Payees_BudgetId_NormalizedName",
                table: "Payees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningStates",
                table: "LongTermPlanningStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningIncomes",
                table: "LongTermPlanningIncomes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LongTermPlanningCategories",
                table: "LongTermPlanningCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LedgerStates",
                table: "LedgerStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagDefinitions",
                table: "FlagDefinitions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "ScheduleOccurrences");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "RegisterViews");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Reconciliations");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "LongTermPlanningStates");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "LongTermPlanningIncomes");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "LongTermPlanningCategories");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "LedgerStates");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "LedgerCommands");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "FlagDefinitions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Accounts");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LedgerStates",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RegisterViews",
                table: "RegisterViews",
                column: "Scope");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningStates",
                table: "LongTermPlanningStates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningIncomes",
                table: "LongTermPlanningIncomes",
                column: "Month");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LongTermPlanningCategories",
                table: "LongTermPlanningCategories",
                columns: new[] { "Month", "CategoryId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LedgerStates",
                table: "LedgerStates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagDefinitions",
                table: "FlagDefinitions",
                column: "Color");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_AccountId_NextDate",
                table: "Schedules",
                columns: new[] { "AccountId", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOccurrences_ScheduleId_Sequence",
                table: "ScheduleOccurrences",
                columns: new[] { "ScheduleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payees_NormalizedName",
                table: "Payees",
                column: "NormalizedName",
                unique: true);
        }
    }
}
