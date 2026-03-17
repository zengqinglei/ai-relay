using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveUpRequestUrlToMainTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpRequestUrl",
                table: "UsageRecordDetails");

            migrationBuilder.AddColumn<string>(
                name: "UpRequestUrl",
                table: "UsageRecords",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpRequestUrl",
                table: "UsageRecords");

            migrationBuilder.AddColumn<string>(
                name: "UpRequestUrl",
                table: "UsageRecordDetails",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
