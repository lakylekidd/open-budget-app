using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgentTokenNoExpiryAndRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "AgentTokens",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RenewedAt",
                table: "AgentTokens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenewedAt",
                table: "AgentTokens");

            // Rolling back cannot represent "no expiration": fail closed by expiring those tokens now.
            migrationBuilder.Sql("UPDATE \"AgentTokens\" SET \"ExpiresAt\" = now() WHERE \"ExpiresAt\" IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "AgentTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
