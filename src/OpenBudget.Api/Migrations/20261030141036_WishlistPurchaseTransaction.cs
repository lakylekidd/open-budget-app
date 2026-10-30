using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class WishlistPurchaseTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseTransactionId",
                table: "WishlistItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_PurchaseTransactionId",
                table: "WishlistItems",
                column: "PurchaseTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WishlistItems_PurchaseTransactionId",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "PurchaseTransactionId",
                table: "WishlistItems");
        }
    }
}
