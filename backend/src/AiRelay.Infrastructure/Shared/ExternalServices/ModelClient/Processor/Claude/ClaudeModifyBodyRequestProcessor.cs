using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

/// <summary>
/// Claude 请求体处理器：模型 ID 写入、黑名单系统提示词过滤、cache_control 数量限制
/// </summary>
public class ClaudeModifyBodyRequestProcessor(
    ChatModelConnectionOptions options,
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IClaudeCodeClientDetector clientDetector) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (down.BodyJsonNode is not JsonObject || down.BodyJsonNode == null)
        {
            return Task.CompletedTask;
        }

        var clonedBody = down.CloneBodyJson() ?? [];

        // 写入 mapped model id
        if (!string.IsNullOrEmpty(up.MappedModelId) && down.ModelId != up.MappedModelId)
        {
            clonedBody["model"] = up.MappedModelId;
        }

        // 聊天接口特有处理
        if (up.RelativePath.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase))
        {
            bool isOAuth = options.Platform == ProviderPlatform.CLAUDE_OAUTH;

            // OAuth 专属：过滤黑名单系统提示词
            if (isOAuth)
                claudeRequestCleaner.FilterSystemBlocksByPrefix(clonedBody);

            // 强制执行 cache_control 块数量限制（最多 4 个）
            claudeCacheControlCleaner.EnforceCacheControlLimit(clonedBody);

            // 伪装逻辑：inject Claude Code system prompt + metadata
            bool shouldMimic = options.ShouldMimicOfficialClient;
            bool isClaudeCodeClient = clientDetector.IsClaudeCodeClient(down, clonedBody);
            bool isHaikuModel = !string.IsNullOrEmpty(up.MappedModelId) &&
                                up.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

            if (shouldMimic && !isClaudeCodeClient && !isHaikuModel)
            {
                claudeSystemPromptInjector.InjectClaudeCodePrompt(clonedBody);
            }

        }

        up.BodyJson = clonedBody;
        return Task.CompletedTask;
    }
}
