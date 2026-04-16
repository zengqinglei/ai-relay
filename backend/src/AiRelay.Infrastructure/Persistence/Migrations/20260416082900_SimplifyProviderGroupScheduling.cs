using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyProviderGroupScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchedulingStrategy",
                table: "ProviderGroups");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "ProviderGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AccountTokens",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                table: "AccountTokens",
                type: "integer",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "ProviderGroups");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "AccountTokens");

            migrationBuilder.AddColumn<string>(
                name: "SchedulingStrategy",
                table: "ProviderGroups",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ProviderGroupAccountRelations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                table: "ProviderGroupAccountRelations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
