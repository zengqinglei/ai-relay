using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeleteBehaviorToCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ApiKeys_ApiKeyId",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderGroupAccountRelations_AccountTokens_AccountTokenId",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderGroupAccountRelations_ProviderGroups_ProviderGroupId",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ApiKeys_ApiKeyId",
                table: "ApiKeyProviderGroupBindings",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderGroupAccountRelations_AccountTokens_AccountTokenId",
                table: "ProviderGroupAccountRelations",
                column: "AccountTokenId",
                principalTable: "AccountTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderGroupAccountRelations_ProviderGroups_ProviderGroupId",
                table: "ProviderGroupAccountRelations",
                column: "ProviderGroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ApiKeys_ApiKeyId",
                table: "ApiKeyProviderGroupBindings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderGroupAccountRelations_AccountTokens_AccountTokenId",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderGroupAccountRelations_ProviderGroups_ProviderGroupId",
                table: "ProviderGroupAccountRelations");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyProviderGroupBindings_ApiKeys_ApiKeyId",
                table: "ApiKeyProviderGroupBindings",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderGroupAccountRelations_AccountTokens_AccountTokenId",
                table: "ProviderGroupAccountRelations",
                column: "AccountTokenId",
                principalTable: "AccountTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderGroupAccountRelations_ProviderGroups_ProviderGroupId",
                table: "ProviderGroupAccountRelations",
                column: "ProviderGroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
