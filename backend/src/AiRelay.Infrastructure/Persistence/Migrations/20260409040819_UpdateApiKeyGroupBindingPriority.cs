using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApiKeyGroupBindingPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_RouteProfile_DeletionT~",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.DropColumn(
                name: "RouteProfile",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ApiKeyProviderGroupBindings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_ProviderGroupId_Deleti~",
                table: "ApiKeyProviderGroupBindings",
                columns: new[] { "ApiKeyId", "ProviderGroupId", "DeletionTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_ProviderGroupId_Deleti~",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.AddColumn<string>(
                name: "RouteProfile",
                table: "ApiKeyProviderGroupBindings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_RouteProfile_DeletionT~",
                table: "ApiKeyProviderGroupBindings",
                columns: new[] { "ApiKeyId", "RouteProfile", "DeletionTime" },
                unique: true);
        }
    }
}
