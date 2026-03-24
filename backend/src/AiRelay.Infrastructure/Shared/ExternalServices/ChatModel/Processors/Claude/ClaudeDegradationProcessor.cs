using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude 降级处理器
/// Level 1: 移除 thinking 块配置并转换 thinking 块
/// Level 2+: 在 Level 1 基础上，转换 tool_use/tool_result 块
/// </summary>
public class ClaudeDegradationProcessor(
    int degradationLevel,
    ClaudeThinkingCleaner claudeThinkingCleaner,
    ILogger logger) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (up.BodyJson == null || degradationLevel == 0)
        {
            return Task.CompletedTask;
        }

        if (degradationLevel == 1)
        {
            if (claudeThinkingCleaner.FilterThinkingBlocks(up.BodyJson))
                logger.LogWarning("应用降级级别 1: 移除 thinking 配置并转换 thinking 块");
        }
        else if (degradationLevel >= 2)
        {
            if (claudeThinkingCleaner.FilterSignatureSensitiveBlocks(up.BodyJson))
                logger.LogWarning("应用降级级别 2: 转换所有签名敏感块（thinking + tools）");
        }

        return Task.CompletedTask;
    }
}
