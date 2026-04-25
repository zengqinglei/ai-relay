using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUserIdForHistoricalApiKeysAndUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE admin_user_id uuid;
                BEGIN
                    SELECT "Id"
                    INTO admin_user_id
                    FROM "Users"
                    WHERE "Username" = 'admin'
                      AND "DeletionTime" IS NULL
                    ORDER BY "CreationTime" ASC
                    LIMIT 1;

                    IF admin_user_id IS NULL THEN
                        RAISE NOTICE 'Skip historical user id backfill because admin user was not found.';
                        RETURN;
                    END IF;

                    UPDATE "ApiKeys"
                    SET "UserId" = admin_user_id
                    WHERE "UserId" = '00000000-0000-0000-0000-000000000000';

                    UPDATE "UsageRecords"
                    SET "UserId" = admin_user_id
                    WHERE "UserId" = '00000000-0000-0000-0000-000000000000';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
