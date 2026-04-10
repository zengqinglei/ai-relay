using AiRelay.Domain.ApiKeys.Entities;
using Leistd.Ddd.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.EntityConfigurations;

internal static class ApiKeyEntityConfiguration
{
    internal static void ConfigureApiKeys(this ModelBuilder builder)
    {
        builder.ConfigureApiKeyEntities();
        builder.ConfigureApiKeyProviderGroupBindings();
    }

    private static void ConfigureApiKeyEntities(this ModelBuilder builder)
    {
        builder.Entity<ApiKey>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.Name).IsRequired().HasMaxLength(256);
            b.Property(e => e.Description).HasMaxLength(1024);
            b.Property(e => e.EncryptedSecret).IsRequired().HasMaxLength(512);
            b.Property(e => e.SecretHash).IsRequired().HasMaxLength(64);

            b.Property(e => e.CostToday).HasPrecision(18, 8);
            b.Property(e => e.CostTotal).HasPrecision(18, 8);

            b.HasIndex(e => e.SecretHash).IsUnique();
        });
    }

    private static void ConfigureApiKeyProviderGroupBindings(this ModelBuilder builder)
    {
        builder.Entity<ApiKeyProviderGroupBinding>(b =>
        {
            b.ConfigureByConvention();

            b.HasIndex(e => new { e.ApiKeyId, e.ProviderGroupId, e.DeletionTime }).IsUnique();

            b.HasOne(e => e.ApiKey)
                .WithMany(ak => ak.Bindings)
                .HasForeignKey(e => e.ApiKeyId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne(e => e.ProviderGroup)
                .WithMany(pg => pg.ApiKeyBindings)
                .HasForeignKey(e => e.ProviderGroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
