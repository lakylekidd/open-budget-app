using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnableBankingConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankImportId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankAuthorizationStates",
                columns: table => new
                {
                    State = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AspspName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAuthorizationStates", x => x.State);
                });

            migrationBuilder.CreateTable(
                name: "BankConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AspspName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ProtectedSessionId = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AccessValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    NextSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SyncLeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastImportedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankConnections_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkedBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderAccountUid = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdentificationHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkedBankAccounts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LinkedBankAccounts_BankConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LinkedBankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EntryReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Payee = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Memo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CreatedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankImports_LinkedBankAccounts_LinkedBankAccountId",
                        column: x => x.LinkedBankAccountId,
                        principalTable: "LinkedBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BankImportId",
                table: "Transactions",
                column: "BankImportId",
                unique: true,
                filter: "\"BankImportId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankAuthorizationStates_ExpiresAt",
                table: "BankAuthorizationStates",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_BudgetId_NextSyncAt_Status",
                table: "BankConnections",
                columns: new[] { "BudgetId", "NextSyncAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_BudgetId_OwnerUserId",
                table: "BankConnections",
                columns: new[] { "BudgetId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_OwnerUserId",
                table: "BankConnections",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankImports_BudgetId_ReviewStatus_Date",
                table: "BankImports",
                columns: new[] { "BudgetId", "ReviewStatus", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_BankImports_LinkedBankAccountId_ProviderTransactionId",
                table: "BankImports",
                columns: new[] { "LinkedBankAccountId", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedBankAccounts_AccountId",
                table: "LinkedBankAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedBankAccounts_BudgetId_AccountId",
                table: "LinkedBankAccounts",
                columns: new[] { "BudgetId", "AccountId" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedBankAccounts_BudgetId_IdentificationHash",
                table: "LinkedBankAccounts",
                columns: new[] { "BudgetId", "IdentificationHash" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedBankAccounts_ConnectionId_ProviderAccountUid",
                table: "LinkedBankAccounts",
                columns: new[] { "ConnectionId", "ProviderAccountUid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BankImports_BankImportId",
                table: "Transactions",
                column: "BankImportId",
                principalTable: "BankImports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BankImports_BankImportId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "BankAuthorizationStates");

            migrationBuilder.DropTable(
                name: "BankImports");

            migrationBuilder.DropTable(
                name: "LinkedBankAccounts");

            migrationBuilder.DropTable(
                name: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BankImportId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BankImportId",
                table: "Transactions");
        }
    }
}
