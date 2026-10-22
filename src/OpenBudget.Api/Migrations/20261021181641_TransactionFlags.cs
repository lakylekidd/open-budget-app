using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class TransactionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagColor",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlagColor",
                table: "Schedules",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlagDefinitions",
                columns: table => new
                {
                    Color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagDefinitions", x => x.Color);
                });

            migrationBuilder.InsertData(
                table: "FlagDefinitions",
                columns: new[] { "Color", "Name" },
                values: new object[,]
                {
                    { "blue", "Blue" },
                    { "green", "Green" },
                    { "orange", "Orange" },
                    { "purple", "Purple" },
                    { "red", "Red" },
                    { "yellow", "Yellow" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlagDefinitions");

            migrationBuilder.DropColumn(
                name: "FlagColor",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FlagColor",
                table: "Schedules");
        }
    }
}
