using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

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
            bool isClaudeCodeClient = clientDetector.IsClaudeCodeClient(down);
            bool isHaikuModel = !string.IsNullOrEmpty(up.MappedModelId) &&
                                up.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

            if (shouldMimic && !isClaudeCodeClient && !isHaikuModel)
            {
                claudeSystemPromptInjector.InjectClaudeCodePrompt(clonedBody);
            }

        }
    }
}
