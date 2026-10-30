using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class LoanAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanTerms",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    MinimumPayment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EscrowAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PairedCategoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    BalanceConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContractMonths = table.Column<int>(type: "integer", nullable: true),
                    ContractStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IncludesDevice = table.Column<bool>(type: "boolean", nullable: false),
                    UpfrontCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    MonthlyServiceCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DeviceTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountMonths = table.Column<int>(type: "integer", nullable: false),
                    DiscountedServiceCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanTerms", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_LoanTerms_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanTerms_BudgetId_PairedCategoryId",
                table: "LoanTerms",
                columns: new[] { "BudgetId", "PairedCategoryId" },
                unique: true,
                filter: "\"PairedCategoryId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanTerms");
        }
    }
}
