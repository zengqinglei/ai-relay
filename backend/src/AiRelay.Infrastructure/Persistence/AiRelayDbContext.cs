using AiRelay.Domain.Auth.Entities;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Permissions.Entities;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Infrastructure.Persistence.EntityConfigurations;

namespace AiRelay.Infrastructure.Persistence;

public class AiRelayDbContext(
    DbContextOptions<AiRelayDbContext> options,
    IServiceProvider? serviceProvider) : BaseDbContext(options, serviceProvider)
{
    public DbSet<AccountToken> AccountTokens { get; set; } = null!;
    public DbSet<AccountFingerprint> AccountFingerprints { get; set; } = null!;
    public DbSet<UsageRecord> UsageRecords { get; set; } = null!;
    public DbSet<UsageRecordDetail> UsageRecordDetails { get; set; } = null!;
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;

    // Provider Groups
    public DbSet<ProviderGroup> ProviderGroups { get; set; } = null!;
    public DbSet<ProviderGroupAccountRelation> ProviderGroupAccountRelations { get; set; } = null!;
    public DbSet<ApiKeyProviderGroupBinding> ApiKeyProviderGroupBindings { get; set; } = null!;

    // Identity
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<PermissionGrant> PermissionGrants { get; set; } = null!;
    public DbSet<ExternalLoginConnection> ExternalLoginConnections { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 静态配置：属性约束、索引、关系
        modelBuilder.ConfigureIdentity();
        modelBuilder.ConfigureApiKeys();
        modelBuilder.ConfigureProviders();
        modelBuilder.ConfigureUsageRecords();
    }
}
