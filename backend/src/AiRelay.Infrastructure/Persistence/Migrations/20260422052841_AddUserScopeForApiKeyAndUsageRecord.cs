using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopeForApiKeyAndUsageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyName",
                table: "UsageRecords",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApiKeyId",
                table: "UsageRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "UsageRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "UsageRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ApiKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_UserId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "UserId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_UserId_Source_CreationTime",
                table: "UsageRecords",
                columns: new[] { "UserId", "Source", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId_CreationTime",
                table: "ApiKeys",
                columns: new[] { "UserId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId_Name",
                table: "ApiKeys",
                columns: new[] { "UserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_UserId_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_UserId_Source_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_UserId_CreationTime",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_UserId_Name",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ApiKeys");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyName",
                table: "UsageRecords",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApiKeyId",
                table: "UsageRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
