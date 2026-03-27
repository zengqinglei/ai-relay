using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUsageRecordUpModelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ProviderGroupId column and FK were already removed in RefactorUsageRecordToAttempt migration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderGroupId",
                table: "UsageRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageRecords_ProviderGroups_ProviderGroupId",
                table: "UsageRecords",
                column: "ProviderGroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
