using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude OAuth 专属：注入 metadata.user_id（基于账号指纹）
/// 原 Middleware.InjectMetadataUserIdAsync 迁移至此，操作 up.BodyJson（CloneBodyJson 后的副本）
/// </summary>
public class ClaudeMetadataInjectProcessor(
    ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // Claude OAuth 专属：fingerprint user_id 注入
        // 仅 OAuth 且非 batches 路由
        if (options.Platform != ProviderPlatform.CLAUDE_OAUTH
            || down.RelativePath.Contains("/batches", StringComparison.OrdinalIgnoreCase)
            || up.BodyJson == null)
        {
            return Task.CompletedTask;
        }

        // 仅在 metadata.user_id 为空时注入
        if (up.BodyJson.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadataObj &&
            metadataObj.TryGetPropertyValue("user_id", out var userIdNode) &&
            userIdNode is JsonValue userIdVal &&
            userIdVal.TryGetValue<string>(out var existingUserId) &&
            !string.IsNullOrWhiteSpace(existingUserId))
        {
            return Task.CompletedTask;
        }

        var clientId = down.FingerprintClientId ?? "unknown";
        var sessionId = down.StickySessionId ?? Guid.NewGuid().ToString("D");
        options.ExtraProperties.TryGetValue("account_uuid", out var accountUuid);

        var userId = !string.IsNullOrWhiteSpace(accountUuid)
            ? $"user_{clientId}_account_{accountUuid.Trim()}_session_{sessionId}"
            : $"user_{clientId}_account__session_{sessionId}";

        if (!up.BodyJson.ContainsKey("metadata"))
            up.BodyJson["metadata"] = new JsonObject();

        if (up.BodyJson["metadata"] is JsonObject metadata)
            metadata["user_id"] = userId;

        return Task.CompletedTask;
    }
}
