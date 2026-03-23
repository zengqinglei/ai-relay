using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude 请求体处理器：模型 ID 写入、黑名单系统提示词过滤、cache_control 数量限制
/// </summary>
public class ClaudeRequestBodyProcessor(
    ChatModelConnectionOptions options,
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IClaudeCodeClientDetector clientDetector) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var requestJson = down.CloneBodyJson();
        if (requestJson == null)
        {
            up.BodyJson = null;
            return Task.CompletedTask;
        }

        // 写入 mapped model id
        if (!string.IsNullOrEmpty(up.MappedModelId))
            requestJson["model"] = up.MappedModelId;

        bool isOAuth = options.Platform == ProviderPlatform.CLAUDE_OAUTH;

        // OAuth 专属：过滤黑名单系统提示词
        if (isOAuth)
            claudeRequestCleaner.FilterSystemBlocksByPrefix(requestJson);

        // 强制执行 cache_control 块数量限制（最多 4 个）
        claudeCacheControlCleaner.EnforceCacheControlLimit(requestJson);

        // 伪装逻辑：inject Claude Code system prompt + metadata
        bool shouldMimic = options.ShouldMimicOfficialClient;
        bool isClaudeCodeClient = clientDetector.IsClaudeCodeClient(down, requestJson);
        bool isHaikuModel = !string.IsNullOrEmpty(up.MappedModelId) &&
                            up.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

        if (shouldMimic && !isClaudeCodeClient && !isHaikuModel)
        {
            claudeSystemPromptInjector.InjectClaudeCodePrompt(requestJson);
            InjectClaudeCodeMetadata(requestJson);
        }

        up.BodyJson = requestJson;
        return Task.CompletedTask;
    }

    private static void InjectClaudeCodeMetadata(JsonObject requestJson)
    {
        if (!requestJson.ContainsKey("metadata"))
            requestJson["metadata"] = new JsonObject();

        if (requestJson["metadata"] is JsonObject metadata && !metadata.ContainsKey("user_id"))
        {
            var randomBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
            var hex64 = Convert.ToHexString(randomBytes).ToLowerInvariant();
            metadata["user_id"] = $"user_{hex64}_account__session_{Guid.NewGuid():N}";
        }
    }
}
