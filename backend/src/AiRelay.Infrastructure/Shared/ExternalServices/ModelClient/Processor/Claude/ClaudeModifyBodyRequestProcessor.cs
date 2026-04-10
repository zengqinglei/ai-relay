using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

/// <summary>
/// Claude 请求体处理器：模型 ID 写入、黑名单系统提示词过滤、cache_control 数量限制、metadata.user_id 注入
/// </summary>
public class ClaudeModifyBodyRequestProcessor(
    ChatModelConnectionOptions options,
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IClaudeCodeClientDetector clientDetector) : IRequestProcessor
{

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        bool needChangeModel = !string.IsNullOrEmpty(up.MappedModelId) && down.ModelId != up.MappedModelId;
        bool isMessagesRoute = up.RelativePath.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase);

        // 如果既不需要改模型，又不是聊天生成接口，则无需解析 Body，直接走零分配转发
        if (!needChangeModel && !isMessagesRoute)
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);

        // 写入 mapped model id
        if (needChangeModel)
        {
            clonedBody["model"] = up.MappedModelId;
        }

        // 聊天接口特有处理
        if (isMessagesRoute)
        {
            bool isOAuth = options.AuthMethod == AuthMethod.OAuth;
            bool shouldMimic = options.ShouldMimicOfficialClient;

            // OAuth 专属：过滤黑名单系统提示词
            if (isOAuth)
                claudeRequestCleaner.FilterSystemBlocksByPrefix(clonedBody);

            // 强制执行 cache_control 块数量限制（最多 4 个）
            claudeCacheControlCleaner.EnforceCacheControlLimit(clonedBody);

            // 伪装逻辑：inject Claude Code system prompt
            bool isClaudeCodeClient = clientDetector.IsClaudeCodeClient(down);
            bool isHaikuModel = (up.MappedModelId ?? down.ModelId ?? "")
                                .Contains("haiku", StringComparison.OrdinalIgnoreCase);

            if (shouldMimic && !isClaudeCodeClient && !isHaikuModel)
            {
                claudeSystemPromptInjector.InjectClaudeCodePrompt(clonedBody);
            }

            // 注入 metadata.user_id（OAuth 或开启伪装时执行，非 batches 路由）
            if ((isOAuth || shouldMimic) &&
                !down.RelativePath.Contains("/batches", StringComparison.OrdinalIgnoreCase))
            {
                InjectMetadataUserId(down, clonedBody);
            }
        }
    }

    private void InjectMetadataUserId(DownRequestContext down, JsonObject clonedBody)
    {
        // 零分配捷径：ExtractedProps 中已有有效的 metadata.user_id，直接短路
        if (down.ExtractedProps.TryGetValue("metadata.user_id", out var extUserId) && !string.IsNullOrWhiteSpace(extUserId))
            return;

        // Body 中已有非空的 metadata.user_id，直接短路
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
