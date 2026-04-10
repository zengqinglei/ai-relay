using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace AiRelay.Infrastructure.Persistence.EntityConfigurations;

internal static class ProviderEntityConfiguration
{
    internal static void ConfigureProviders(this ModelBuilder builder)
    {
        builder.ConfigureAccountTokens();
        builder.ConfigureAccountFingerprints();
        builder.ConfigureProviderGroups();
        builder.ConfigureProviderGroupAccountRelations();
    }

    private static void ConfigureAccountTokens(this ModelBuilder builder)
    {
        builder.Entity<AccountToken>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.Name).IsRequired().HasMaxLength(256);
            b.Property(e => e.AccessToken).HasMaxLength(4096);
            b.Property(e => e.RefreshToken).HasMaxLength(2048);
            b.Property(e => e.BaseUrl).HasMaxLength(512);
            b.Property(e => e.Description).HasMaxLength(1024);
            b.Property(e => e.StatusDescription).HasMaxLength(512);

            b.Property(e => e.ExtraProperties)
                .HasMaxLength(4096)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                         ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToDictionary(e => e.Key, e => e.Value)));

            b.Property(e => e.ModelWhites)
                .HasMaxLength(4096)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
                .Metadata.SetValueComparer(new ValueComparer<List<string>?>(
                    (c1, c2) => c1 == c2 || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : c.ToList()));

            b.Property(e => e.ModelMapping)
                .HasMaxLength(4096)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null))
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>?>(
                    (c1, c2) => c1 == c2 || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : c.ToDictionary(e => e.Key, e => e.Value)));

            b.Property(e => e.CostToday).HasPrecision(18, 8);
            b.Property(e => e.CostTotal).HasPrecision(18, 8);

            b.HasIndex(e => new { e.Provider, e.IsActive, e.Status });
        });
    }

    private static void ConfigureAccountFingerprints(this ModelBuilder builder)
    {
        builder.Entity<AccountFingerprint>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.ClientId).IsRequired().HasMaxLength(64);
            b.Property(e => e.UserAgent).IsRequired().HasMaxLength(1024);
            b.Property(e => e.StainlessLang).HasMaxLength(50);
            b.Property(e => e.StainlessPackageVersion).HasMaxLength(50);
            b.Property(e => e.StainlessOS).HasMaxLength(50);
            b.Property(e => e.StainlessArch).HasMaxLength(50);
            b.Property(e => e.StainlessRuntime).HasMaxLength(50);
            b.Property(e => e.StainlessRuntimeVersion).HasMaxLength(50);

            b.HasIndex(e => e.AccountTokenId).IsUnique();
        });
    }

    private static void ConfigureProviderGroups(this ModelBuilder builder)
    {
        builder.Entity<ProviderGroup>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.Name).IsRequired().HasMaxLength(256);
            b.Property(e => e.Description).HasMaxLength(1024);
            b.Property(e => e.RateMultiplier).HasPrecision(10, 4);

            b.HasIndex(e => new { e.Name, e.DeletionTime }).IsUnique();
        });
    }

    private static void ConfigureProviderGroupAccountRelations(this ModelBuilder builder)
    {
        builder.Entity<ProviderGroupAccountRelation>(b =>
        {
            b.ConfigureByConvention();

            b.HasIndex(e => new { e.ProviderGroupId, e.AccountTokenId, e.DeletionTime }).IsUnique();

            b.HasOne(e => e.ProviderGroup)
                .WithMany(pg => pg.Relations)
                .HasForeignKey(e => e.ProviderGroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne(e => e.AccountToken)
                .WithMany()
                .HasForeignKey(e => e.AccountTokenId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
