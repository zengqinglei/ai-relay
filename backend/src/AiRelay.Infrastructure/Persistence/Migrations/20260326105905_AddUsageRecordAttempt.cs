using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageRecordAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsageRecords_AccountTokens_AccountTokenId",
                table: "UsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_AccountTokenId_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_AccountTokenId_Status_CreationTime",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AccountTokenId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "AccountTokenName",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpModelId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpRequestUrl",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpStatusCode",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpUserAgent",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "UpRequestBody",
                table: "UsageRecordDetails");

            migrationBuilder.DropColumn(
                name: "UpRequestHeaders",
                table: "UsageRecordDetails");

            migrationBuilder.DropColumn(
                name: "UpResponseBody",
                table: "UsageRecordDetails");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "UsageRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UsageRecordAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountTokenName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UpRequestUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    UpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StatusDescription = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecordAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecordAttempts_UsageRecords_UsageRecordId",
                        column: x => x.UsageRecordId,
                        principalTable: "UsageRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecordAttemptDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageRecordAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpRequestHeaders = table.Column<string>(type: "text", nullable: true),
                    UpRequestBody = table.Column<string>(type: "text", nullable: true),
                    UpResponseBody = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecordAttemptDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecordAttemptDetails_UsageRecordAttempts_UsageRecordAt~",
                        column: x => x.UsageRecordAttemptId,
                        principalTable: "UsageRecordAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecordAttemptDetails_UsageRecordAttemptId",
                table: "UsageRecordAttemptDetails",
                column: "UsageRecordAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecordAttempts_AccountTokenId_Status",
                table: "UsageRecordAttempts",
                columns: new[] { "AccountTokenId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecordAttempts_UsageRecordId_AttemptNumber",
                table: "UsageRecordAttempts",
                columns: new[] { "UsageRecordId", "AttemptNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageRecordAttemptDetails");

            migrationBuilder.DropTable(
                name: "UsageRecordAttempts");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "UsageRecords");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountTokenId",
                table: "UsageRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AccountTokenName",
                table: "UsageRecords",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpModelId",
                table: "UsageRecords",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpRequestUrl",
                table: "UsageRecords",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpStatusCode",
                table: "UsageRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpUserAgent",
                table: "UsageRecords",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpRequestBody",
                table: "UsageRecordDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpRequestHeaders",
                table: "UsageRecordDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpResponseBody",
                table: "UsageRecordDetails",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_AccountTokenId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "AccountTokenId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_AccountTokenId_Status_CreationTime",
                table: "UsageRecords",
                columns: new[] { "AccountTokenId", "Status", "CreationTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_UsageRecords_AccountTokens_AccountTokenId",
                table: "UsageRecords",
                column: "AccountTokenId",
                principalTable: "AccountTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
