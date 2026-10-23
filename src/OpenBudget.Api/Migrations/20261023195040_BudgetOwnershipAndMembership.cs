using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class BudgetOwnershipAndMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "BudgetPlans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "BudgetPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "BudgetPlans",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MaxActiveBudgets = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMemberships",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MembershipPlanId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BudgetLimitOverride = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemberships", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMemberships_MembershipPlans_MembershipPlanId",
                        column: x => x.MembershipPlanId,
                        principalTable: "MembershipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MembershipPlans",
                columns: new[] { "Id", "MaxActiveBudgets", "Name" },
                values: new object[,]
                {
                    { "free", 1, "Free" },
                    { "plus", 3, "Plus" },
                    { "pro", null, "Pro" }
                });

            migrationBuilder.Sql("INSERT INTO \"UserMemberships\" (\"UserId\", \"MembershipPlanId\", \"Version\") SELECT \"Id\", 'free', 1 FROM \"AspNetUsers\"");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_OwnerUserId_IsArchived",
                table: "BudgetPlans",
                columns: new[] { "OwnerUserId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemberships_MembershipPlanId",
                table: "UserMemberships",
                column: "MembershipPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlans_AspNetUsers_OwnerUserId",
                table: "BudgetPlans",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPlans_AspNetUsers_OwnerUserId",
                table: "BudgetPlans");

            migrationBuilder.DropTable(
                name: "UserMemberships");

            migrationBuilder.DropTable(
                name: "MembershipPlans");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPlans_OwnerUserId_IsArchived",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "BudgetPlans");
        }
    }
}
