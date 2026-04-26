using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateProviderGroupAssignedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderGroupAssignedUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifierId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderGroupAssignedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderGroupAssignedUsers_ProviderGroups_ProviderGroupId",
                        column: x => x.ProviderGroupId,
                        principalTable: "ProviderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroupAssignedUsers_ProviderGroupId_UserId",
                table: "ProviderGroupAssignedUsers",
                columns: new[] { "ProviderGroupId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderGroupAssignedUsers");
        }
    }
}
