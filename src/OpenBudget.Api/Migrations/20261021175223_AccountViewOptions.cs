using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountViewOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisterViews",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    DatePreset = table.Column<string>(type: "text", nullable: false),
                    FromMonth = table.Column<string>(type: "text", nullable: true),
                    ToMonth = table.Column<string>(type: "text", nullable: true),
                    ShowReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    ShowRunningBalance = table.Column<bool>(type: "boolean", nullable: false),
                    ShowFlags = table.Column<bool>(type: "boolean", nullable: false),
                    ShowImages = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterViews", x => x.Scope);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegisterViews");
        }
    }
}
