using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvalidated",
                table: "Reconciliations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceIndex = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Repeat = table.Column<string>(type: "text", nullable: false),
                    TemplateJson = table.Column<string>(type: "text", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnded = table.Column<bool>(type: "boolean", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schedules_Accounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeferredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OverrideJson = table.Column<string>(type: "text", nullable: true),
                    Disposition = table.Column<string>(type: "text", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleOccurrences_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOccurrences_ScheduleId_Sequence",
                table: "ScheduleOccurrences",
                columns: new[] { "ScheduleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_AccountId_NextDate",
                table: "Schedules",
                columns: new[] { "AccountId", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ToAccountId",
                table: "Schedules",
                column: "ToAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleOccurrences");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsInvalidated",
                table: "Reconciliations");
        }
    }
}
