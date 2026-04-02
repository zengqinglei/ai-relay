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

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 非 batches 路由
        if (down.RelativePath.Contains("/batches", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 零分配捷径：如果底层提取池中已经包含有效的 metadata.user_id，则直接短路，完美零分配
        if (down.ExtractedProps.TryGetValue("metadata.user_id", out var extUserId) && !string.IsNullOrWhiteSpace(extUserId))
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);

        // 仅在 metadata.user_id 为空时注入
        if (clonedBody.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadataObj &&
            metadataObj.TryGetPropertyValue("user_id", out var userIdNode) &&
            userIdNode is JsonValue userIdVal &&
            userIdVal.TryGetValue<string>(out var existingUserId) &&
            !string.IsNullOrWhiteSpace(existingUserId))
        {
            return;
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

        if (!clonedBody.ContainsKey("metadata"))
            clonedBody["metadata"] = new JsonObject();

        if (clonedBody["metadata"] is JsonObject metadata)
            metadata["user_id"] = userIdObj.ToJsonString();
        }
}
