using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class Wishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WishlistBuckets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistBuckets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishlistBuckets_BudgetPlans_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistSettings",
                columns: table => new
                {
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistSettings", x => x.BudgetId);
                    table.ForeignKey(
                        name: "FK_WishlistSettings_BudgetPlans_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BucketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishlistItems_WishlistBuckets_BucketId",
                        column: x => x.BucketId,
                        principalTable: "WishlistBuckets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MembershipPlanEntitlements",
                columns: new[] { "EntitlementKey", "MembershipPlanId", "IsEnabled" },
                values: new object[,]
                {
                    { "wishlist", "free", false },
                    { "wishlist", "plus", true },
                    { "wishlist", "pro", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistBuckets_BudgetId_IsDeleted_SortOrder",
                table: "WishlistBuckets",
                columns: new[] { "BudgetId", "IsDeleted", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_BucketId_Status_SortOrder",
                table: "WishlistItems",
                columns: new[] { "BucketId", "Status", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_BudgetId",
                table: "WishlistItems",
                column: "BudgetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WishlistItems");

            migrationBuilder.DropTable(
                name: "WishlistSettings");

            migrationBuilder.DropTable(
                name: "WishlistBuckets");

            migrationBuilder.DeleteData(
                table: "MembershipPlanEntitlements",
                keyColumns: new[] { "EntitlementKey", "MembershipPlanId" },
                keyValues: new object[] { "wishlist", "free" });

            migrationBuilder.DeleteData(
                table: "MembershipPlanEntitlements",
                keyColumns: new[] { "EntitlementKey", "MembershipPlanId" },
                keyValues: new object[] { "wishlist", "plus" });

            migrationBuilder.DeleteData(
                table: "MembershipPlanEntitlements",
                keyColumns: new[] { "EntitlementKey", "MembershipPlanId" },
                keyValues: new object[] { "wishlist", "pro" });
        }
    }
}
