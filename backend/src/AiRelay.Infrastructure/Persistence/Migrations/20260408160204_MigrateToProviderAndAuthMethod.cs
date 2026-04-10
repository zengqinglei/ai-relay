using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToProviderAndAuthMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderGroups_Name_Platform",
                table: "ProviderGroups");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "ProviderGroups");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "UsageRecords",
                newName: "Provider");

            migrationBuilder.RenameIndex(
                name: "IX_UsageRecords_Platform_Status_CreationTime",
                table: "UsageRecords",
                newName: "IX_UsageRecords_Provider_Status_CreationTime");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "ApiKeyProviderGroupBindings",
                newName: "RouteProfile");

            migrationBuilder.RenameIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_Platform_DeletionTime",
                table: "ApiKeyProviderGroupBindings",
                newName: "IX_ApiKeyProviderGroupBindings_ApiKeyId_RouteProfile_DeletionT~");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "AccountTokens",
                newName: "Provider");

            migrationBuilder.RenameIndex(
                name: "IX_AccountTokens_Platform_IsActive_Status",
                table: "AccountTokens",
                newName: "IX_AccountTokens_Provider_IsActive_Status");

            migrationBuilder.AddColumn<string>(
                name: "AuthMethod",
                table: "UsageRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthMethod",
                table: "AccountTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // --- Data Migration SQL ---
            
            // Map AccountTokens
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'OpenAI' WHERE \"Provider\" = 'OPENAI_OAUTH'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'OpenAI' WHERE \"Provider\" = 'OPENAI_APIKEY'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Gemini' WHERE \"Provider\" = 'GEMINI_OAUTH'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'Gemini' WHERE \"Provider\" = 'GEMINI_APIKEY'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Claude' WHERE \"Provider\" = 'CLAUDE_OAUTH'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'Claude' WHERE \"Provider\" = 'CLAUDE_APIKEY'");
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Antigravity' WHERE \"Provider\" = 'ANTIGRAVITY'");
            
            // Map UsageRecords
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'OpenAI' WHERE \"Provider\" = 'OPENAI_OAUTH'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'OpenAI' WHERE \"Provider\" = 'OPENAI_APIKEY'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Gemini' WHERE \"Provider\" = 'GEMINI_OAUTH'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'Gemini' WHERE \"Provider\" = 'GEMINI_APIKEY'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Claude' WHERE \"Provider\" = 'CLAUDE_OAUTH'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'ApiKey', \"Provider\" = 'Claude' WHERE \"Provider\" = 'CLAUDE_APIKEY'");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'OAuth', \"Provider\" = 'Antigravity' WHERE \"Provider\" = 'ANTIGRAVITY'");

            // Default for any remaining (e.g. if we had simple ProviderPlatform.OPENAI)
            migrationBuilder.Sql("UPDATE \"AccountTokens\" SET \"AuthMethod\" = 'ApiKey' WHERE \"AuthMethod\" = ''");
            migrationBuilder.Sql("UPDATE \"UsageRecords\" SET \"AuthMethod\" = 'ApiKey' WHERE \"AuthMethod\" = ''");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroups_Name_DeletionTime",
                table: "ProviderGroups",
                columns: new[] { "Name", "DeletionTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderGroups_Name_DeletionTime",
                table: "ProviderGroups");

            migrationBuilder.DropColumn(
                name: "AuthMethod",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AuthMethod",
                table: "AccountTokens");

            migrationBuilder.RenameColumn(
                name: "Provider",
                table: "UsageRecords",
                newName: "Platform");

            migrationBuilder.RenameIndex(
                name: "IX_UsageRecords_Provider_Status_CreationTime",
                table: "UsageRecords",
                newName: "IX_UsageRecords_Platform_Status_CreationTime");

            migrationBuilder.RenameColumn(
                name: "RouteProfile",
                table: "ApiKeyProviderGroupBindings",
                newName: "Platform");

            migrationBuilder.RenameIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_RouteProfile_DeletionT~",
                table: "ApiKeyProviderGroupBindings",
                newName: "IX_ApiKeyProviderGroupBindings_ApiKeyId_Platform_DeletionTime");

            migrationBuilder.RenameColumn(
                name: "Provider",
                table: "AccountTokens",
                newName: "Platform");

            migrationBuilder.RenameIndex(
                name: "IX_AccountTokens_Provider_IsActive_Status",
                table: "AccountTokens",
                newName: "IX_AccountTokens_Platform_IsActive_Status");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "ProviderGroups",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroups_Name_Platform",
                table: "ProviderGroups",
                columns: new[] { "Name", "Platform" },
                unique: true);
        }
    }
}
