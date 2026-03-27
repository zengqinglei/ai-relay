using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.EntityConfigurations;

internal static class UsageRecordEntityConfiguration
{
    internal static void ConfigureUsageRecords(this ModelBuilder builder)
    {
        builder.ConfigureUsageRecordEntities();
        builder.ConfigureUsageRecordDetails();
        builder.ConfigureUsageRecordAttempts();
        builder.ConfigureUsageRecordAttemptDetails();
    }

    private static void ConfigureUsageRecordEntities(this ModelBuilder builder)
    {
        builder.Entity<UsageRecord>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.CorrelationId).IsRequired().HasMaxLength(64);
            b.Property(e => e.ApiKeyName).IsRequired().HasMaxLength(256);
            b.Property(e => e.DownRequestMethod).IsRequired().HasMaxLength(16);
            b.Property(e => e.DownRequestUrl).IsRequired().HasMaxLength(2048);
            b.Property(e => e.DownModelId).HasMaxLength(128);
            b.Property(e => e.DownClientIp).HasMaxLength(64);
            b.Property(e => e.DownUserAgent).HasMaxLength(1024);
            b.Property(e => e.StatusDescription).HasMaxLength(2048);
            b.Property(e => e.BaseCost).HasPrecision(18, 8);
            b.Property(e => e.FinalCost).HasPrecision(18, 8);

            // 核心复合索引（覆盖高频查询场景）
            b.HasIndex(e => new { e.ApiKeyId, e.CreationTime });
            b.HasIndex(e => new { e.ApiKeyId, e.Status, e.CreationTime });
            b.HasIndex(e => new { e.Platform, e.Status, e.CreationTime });

            b.HasOne(e => e.ApiKey)
                .WithMany()
                .HasForeignKey(e => e.ApiKeyId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.HasOne(e => e.Detail)
                .WithOne()
                .HasForeignKey<UsageRecordDetail>(e => e.UsageRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.Attempts)
                .WithOne()
                .HasForeignKey(e => e.UsageRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUsageRecordDetails(this ModelBuilder builder)
    {
        builder.Entity<UsageRecordDetail>(b =>
        {
            b.ConfigureByConvention();

            b.HasIndex(e => e.UsageRecordId).IsUnique();
        });
    }

    private static void ConfigureUsageRecordAttempts(this ModelBuilder builder)
    {
        builder.Entity<UsageRecordAttempt>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.AccountTokenName).IsRequired().HasMaxLength(256);
            b.Property(e => e.ProviderGroupName).HasMaxLength(256);
            b.Property(e => e.GroupRateMultiplier).HasPrecision(10, 4);
            b.Property(e => e.UpModelId).HasMaxLength(128);
            b.Property(e => e.UpUserAgent).HasMaxLength(1024);
            b.Property(e => e.UpRequestUrl).HasMaxLength(2048);
            b.Property(e => e.StatusDescription).HasMaxLength(2048);

            // 主查询索引
            b.HasIndex(e => new { e.UsageRecordId, e.AttemptNumber });
            // 账号维度分析索引
            b.HasIndex(e => new { e.AccountTokenId, e.Status });
            // 分组过滤索引
            b.HasIndex(e => new { e.ProviderGroupId, e.UsageRecordId });

            b.HasOne(e => e.Detail)
                .WithOne()
                .HasForeignKey<UsageRecordAttemptDetail>(e => e.UsageRecordAttemptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUsageRecordAttemptDetails(this ModelBuilder builder)
    {
        builder.Entity<UsageRecordAttemptDetail>(b =>
        {
            b.ConfigureByConvention();

            b.HasIndex(e => e.UsageRecordAttemptId).IsUnique();
        });
    }
}
