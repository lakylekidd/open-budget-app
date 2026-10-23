using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipSubscriptionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingProvider",
                table: "UserMemberships",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                table: "UserMemberships",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubscriptionId",
                table: "UserMemberships",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "UserMemberships",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingProvider",
                table: "UserMemberships");

            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                table: "UserMemberships");

            migrationBuilder.DropColumn(
                name: "ExternalSubscriptionId",
                table: "UserMemberships");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "UserMemberships");
        }
    }
}
