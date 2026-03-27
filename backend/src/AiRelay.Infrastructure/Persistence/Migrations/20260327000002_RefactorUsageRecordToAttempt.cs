using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorUsageRecordToAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UsageRecordAttempts: 新增 group 字段
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderGroupId",
                table: "UsageRecordAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderGroupName",
                table: "UsageRecordAttempts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GroupRateMultiplier",
                table: "UsageRecordAttempts",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            // UsageRecordAttempts: 新增分组过滤索引
            migrationBuilder.CreateIndex(
                name: "IX_UsageRecordAttempts_ProviderGroupId_UsageRecordId",
                table: "UsageRecordAttempts",
                columns: new[] { "ProviderGroupId", "UsageRecordId" });

            // UsageRecords: 删除已移至 Attempt 的冗余字段
            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_ProviderGroupId_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AccountTokenId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AccountTokenName",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "GroupRateMultiplier",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ProviderGroupId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ProviderGroupName",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpModelId",
                table: "UsageRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 恢复 UsageRecords 字段
            migrationBuilder.AddColumn<Guid>(
                name: "AccountTokenId",
                table: "UsageRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountTokenName",
                table: "UsageRecords",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GroupRateMultiplier",
                table: "UsageRecords",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderGroupId",
                table: "UsageRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderGroupName",
                table: "UsageRecords",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpModelId",
                table: "UsageRecords",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ProviderGroupId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "ProviderGroupId", "CreationTime" });

            // 恢复 UsageRecordAttempts
            migrationBuilder.DropIndex(
                name: "IX_UsageRecordAttempts_ProviderGroupId_UsageRecordId",
                table: "UsageRecordAttempts");

            migrationBuilder.DropColumn(
                name: "ProviderGroupId",
                table: "UsageRecordAttempts");

            migrationBuilder.DropColumn(
                name: "ProviderGroupName",
                table: "UsageRecordAttempts");

            migrationBuilder.DropColumn(
                name: "GroupRateMultiplier",
                table: "UsageRecordAttempts");
        }
    }
}
