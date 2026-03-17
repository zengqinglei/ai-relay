using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTokenObtainedTimeAndExpiresIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresIn",
                table: "AccountTokens");

            migrationBuilder.RenameColumn(
                name: "TokenObtainedTime",
                table: "AccountTokens",
                newName: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "AccountTokens",
                newName: "TokenObtainedTime");

            migrationBuilder.AddColumn<long>(
                name: "ExpiresIn",
                table: "AccountTokens",
                type: "bigint",
                nullable: true);
        }
    }
}
