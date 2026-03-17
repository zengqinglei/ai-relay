using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountFingerprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    StainlessLang = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StainlessPackageVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StainlessOS = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StainlessArch = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StainlessRuntime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StainlessRuntimeVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_AccountFingerprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExpiresIn = table.Column<long>(type: "bigint", nullable: true),
                    TokenObtainedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RateLimitDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    StatusDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastStatusUpdateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxConcurrency = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
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
                    table.PrimaryKey("PK_AccountTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    EncryptedSecret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatorId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchedulingStrategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EnableStickySession = table.Column<bool>(type: "boolean", nullable: false),
                    StickySessionExpirationHours = table.Column<int>(type: "integer", nullable: false),
                    RateMultiplier = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_ProviderGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsStatic = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Nickname = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LastLoginTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
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
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeyProviderGroupBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderGroupId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ApiKeyProviderGroupBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeyProviderGroupBindings_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiKeyProviderGroupBindings_ProviderGroups_ProviderGroupId",
                        column: x => x.ProviderGroupId,
                        principalTable: "ProviderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderGroupAccountRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ProviderGroupAccountRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderGroupAccountRelations_AccountTokens_AccountTokenId",
                        column: x => x.AccountTokenId,
                        principalTable: "AccountTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderGroupAccountRelations_ProviderGroups_ProviderGroupId",
                        column: x => x.ProviderGroupId,
                        principalTable: "ProviderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountTokenName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderGroupName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GroupRateMultiplier = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    IsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    DownRequestMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DownRequestUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DownModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DownClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DownUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UpModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StatusDescription = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CacheReadTokens = table.Column<int>(type: "integer", nullable: true),
                    CacheCreationTokens = table.Column<int>(type: "integer", nullable: true),
                    BaseCost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    FinalCost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    CreatorId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecords_AccountTokens_AccountTokenId",
                        column: x => x.AccountTokenId,
                        principalTable: "AccountTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageRecords_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageRecords_ProviderGroups_ProviderGroupId",
                        column: x => x.ProviderGroupId,
                        principalTable: "ProviderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLoginConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderAvatarUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ExternalLoginConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLoginConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecordDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DownRequestHeaders = table.Column<string>(type: "text", nullable: true),
                    DownRequestBody = table.Column<string>(type: "text", nullable: true),
                    DownResponseBody = table.Column<string>(type: "text", nullable: true),
                    UpRequestUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    UpRequestHeaders = table.Column<string>(type: "text", nullable: true),
                    UpRequestBody = table.Column<string>(type: "text", nullable: true),
                    UpResponseBody = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecordDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecordDetails_UsageRecords_UsageRecordId",
                        column: x => x.UsageRecordId,
                        principalTable: "UsageRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountFingerprints_AccountTokenId",
                table: "AccountFingerprints",
                column: "AccountTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTokens_Platform_IsActive_Status",
                table: "AccountTokens",
                columns: new[] { "Platform", "IsActive", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyProviderGroupBindings_ApiKeyId_Platform_DeletionTime",
                table: "ApiKeyProviderGroupBindings",
                columns: new[] { "ApiKeyId", "Platform", "DeletionTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyProviderGroupBindings_ProviderGroupId",
                table: "ApiKeyProviderGroupBindings",
                column: "ProviderGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_SecretHash",
                table: "ApiKeys",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginConnections_Provider_ProviderUserId_DeletionTi~",
                table: "ExternalLoginConnections",
                columns: new[] { "Provider", "ProviderUserId", "DeletionTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginConnections_UserId",
                table: "ExternalLoginConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionGrants_PermissionName_ProviderName_ProviderKey",
                table: "PermissionGrants",
                columns: new[] { "PermissionName", "ProviderName", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionGrants_ProviderName_ProviderKey",
                table: "PermissionGrants",
                columns: new[] { "ProviderName", "ProviderKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroupAccountRelations_AccountTokenId",
                table: "ProviderGroupAccountRelations",
                column: "AccountTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroupAccountRelations_ProviderGroupId_AccountTokenI~",
                table: "ProviderGroupAccountRelations",
                columns: new[] { "ProviderGroupId", "AccountTokenId", "DeletionTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroups_Name_Platform",
                table: "ProviderGroups",
                columns: new[] { "Name", "Platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecordDetails_UsageRecordId",
                table: "UsageRecordDetails",
                column: "UsageRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_AccountTokenId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "AccountTokenId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_AccountTokenId_Status_CreationTime",
                table: "UsageRecords",
                columns: new[] { "AccountTokenId", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ApiKeyId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "ApiKeyId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ApiKeyId_Status_CreationTime",
                table: "UsageRecords",
                columns: new[] { "ApiKeyId", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_Platform_Status_CreationTime",
                table: "UsageRecords",
                columns: new[] { "Platform", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ProviderGroupId_CreationTime",
                table: "UsageRecords",
                columns: new[] { "ProviderGroupId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId_DeletionTime",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId", "DeletionTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountFingerprints");

            migrationBuilder.DropTable(
                name: "ApiKeyProviderGroupBindings");

            migrationBuilder.DropTable(
                name: "ExternalLoginConnections");

            migrationBuilder.DropTable(
                name: "PermissionGrants");

            migrationBuilder.DropTable(
                name: "ProviderGroupAccountRelations");

            migrationBuilder.DropTable(
                name: "UsageRecordDetails");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UsageRecords");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AccountTokens");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "ProviderGroups");
        }
    }
}
