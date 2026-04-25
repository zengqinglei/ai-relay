using AiRelay.Domain.ChatSessions.Entities;
using Leistd.Ddd.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.EntityConfigurations;

internal static class ChatSessionEntityConfiguration
{
    internal static void ConfigureChatSessions(this ModelBuilder builder)
    {
        builder.ConfigureChatSessionEntities();
        builder.ConfigureChatMessageEntities();
        builder.ConfigureChatAttachmentEntities();
    }

    private static void ConfigureChatSessionEntities(this ModelBuilder builder)
    {
        builder.Entity<ChatSession>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.Title).IsRequired().HasMaxLength(128);
            b.Property(e => e.ModelId).IsRequired().HasMaxLength(128);
            b.Property(e => e.LastMessagePreview).HasMaxLength(512);

            b.HasIndex(e => new { e.UserId, e.LastMessageTime });
            b.HasIndex(e => new { e.UserId, e.CreationTime });

            b.HasMany(e => e.Messages)
                .WithOne()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureChatMessageEntities(this ModelBuilder builder)
    {
        builder.Entity<ChatMessage>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.Content).IsRequired();
            b.HasIndex(e => new { e.SessionId, e.CreationTime });

            b.HasMany(e => e.Attachments)
                .WithOne()
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureChatAttachmentEntities(this ModelBuilder builder)
    {
        builder.Entity<ChatAttachment>(b =>
        {
            b.ConfigureByConvention();

            b.Property(e => e.MimeType).IsRequired().HasMaxLength(128);
            b.Property(e => e.Url).HasMaxLength(2048);
            b.HasIndex(e => e.MessageId);
        });
    }
}
