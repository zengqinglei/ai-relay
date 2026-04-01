using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

/// <summary>
/// Claude OAuth 专属：注入 metadata.user_id（基于账号指纹）
/// 原 Middleware.InjectMetadataUserIdAsync 迁移至此，操作 up.BodyJson（CloneBodyJson 后的副本）
/// </summary>
public class ClaudeMetadataInjectRequestProcessor(
    ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 非 batches 路由
        if (down.RelativePath.Contains("/batches", StringComparison.OrdinalIgnoreCase) || up.BodyJson == null)
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

        var deviceId = down.FingerprintClientId ?? "unknown";
        var sessionId = down.StickySessionId ?? Guid.NewGuid().ToString("D");
        options.ExtraProperties.TryGetValue("account_uuid", out var accountUuid);

        var userIdObj = new JsonObject
        {
            ["device_id"] = deviceId,
            ["account_uuid"] = accountUuid?.Trim() ?? "",
            ["session_id"] = sessionId
        };

        if (!up.BodyJson.ContainsKey("metadata"))
            up.BodyJson["metadata"] = new JsonObject();

        if (up.BodyJson["metadata"] is JsonObject metadata)
            metadata["user_id"] = userIdObj.ToJsonString();

        return Task.CompletedTask;
    }
}
