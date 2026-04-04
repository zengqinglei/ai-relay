using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownStatusCodeToUsageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownStatusCode",
                table: "UsageRecords",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownStatusCode",
                table: "UsageRecords");
        }
    }
}
