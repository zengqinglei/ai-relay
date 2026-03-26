using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveAllowOfficialClientMimicToAccountToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowOfficialClientMimic",
                table: "ProviderGroups");

            migrationBuilder.AddColumn<bool>(
                name: "AllowOfficialClientMimic",
                table: "AccountTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowOfficialClientMimic",
                table: "AccountTokens");

            migrationBuilder.AddColumn<bool>(
                name: "AllowOfficialClientMimic",
                table: "ProviderGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
