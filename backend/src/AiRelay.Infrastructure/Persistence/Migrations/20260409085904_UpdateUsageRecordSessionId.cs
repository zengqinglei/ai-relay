using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUsageRecordSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_Provider_Status_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AuthMethod",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "UsageRecords");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "UsageRecords",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "UsageRecordAttempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_SessionId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "SessionId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_SessionId_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "UsageRecordAttempts");

            migrationBuilder.AddColumn<string>(
                name: "AuthMethod",
                table: "UsageRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "UsageRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_Provider_Status_CreationTime",
                table: "UsageRecords",
                columns: new[] { "Provider", "Status", "CreationTime" });
        }
    }
}
