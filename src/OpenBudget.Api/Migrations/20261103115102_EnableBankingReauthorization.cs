using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnableBankingReauthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "LinkedBankAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReauthorizationConnectionId",
                table: "BankAuthorizationStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAuthorizationStates_ReauthorizationConnectionId",
                table: "BankAuthorizationStates",
                column: "ReauthorizationConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAuthorizationStates_BankConnections_ReauthorizationConn~",
                table: "BankAuthorizationStates",
                column: "ReauthorizationConnectionId",
                principalTable: "BankConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAuthorizationStates_BankConnections_ReauthorizationConn~",
                table: "BankAuthorizationStates");

            migrationBuilder.DropIndex(
                name: "IX_BankAuthorizationStates_ReauthorizationConnectionId",
                table: "BankAuthorizationStates");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "LinkedBankAccounts");

            migrationBuilder.DropColumn(
                name: "ReauthorizationConnectionId",
                table: "BankAuthorizationStates");
        }
    }
}
