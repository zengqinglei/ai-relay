using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderGroupApiKeyBindingsNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ProviderGroups_ProviderGroupId",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ProviderGroups_ProviderGroupId",
                table: "ApiKeyProviderGroupBindings",
                column: "ProviderGroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ProviderGroups_ProviderGroupId",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ProviderGroups_ProviderGroupId",
                table: "ApiKeyProviderGroupBindings",
                column: "ProviderGroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
