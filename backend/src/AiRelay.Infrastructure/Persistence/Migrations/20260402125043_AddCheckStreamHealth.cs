using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckStreamHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCheckStreamHealth",
                table: "AccountTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCheckStreamHealth",
                table: "AccountTokens");
        }
    }
}
