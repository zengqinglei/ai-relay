using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Claude Cache Control 清洗器
/// 负责强制执行 Anthropic API 的 cache_control 块数量限制（最多 4 个）
/// </summary>
public class ClaudeCacheControlCleaner(ILogger<ClaudeCacheControlCleaner> logger)
{
    private const int MaxCacheControlBlocks = 4; // Anthropic API 允许的最大 cache_control 块数量

    /// <summary>
    /// 强制执行 cache_control 块数量限制（最多 4 个）
    /// </summary>
    public bool EnforceCacheControlLimit(JsonObject requestJson)
    {
        try
        {
            // 1. 清理 thinking 块中的非法 cache_control
            RemoveCacheControlFromThinkingBlocks(requestJson);

            // 2. 计算当前 cache_control 块数量
            int count = CountCacheControlBlocks(requestJson);
            if (count <= MaxCacheControlBlocks)
            {
                return false; // 未超限，无需处理
            }

            logger.LogWarning("检测到 {Count} 个 cache_control 块，超过限制 {Max}，开始移除", count, MaxCacheControlBlocks);

            // 3. 超限：优先从 messages 中移除，再从 system 中移除
            while (count > MaxCacheControlBlocks)
            {
                if (RemoveCacheControlFromMessages(requestJson))
                {
                    count--;
                    continue;
                }
                if (RemoveCacheControlFromSystem(requestJson))
                {
                    count--;
                    continue;
                }
                break; // 无法继续移除
            }

            logger.LogInformation("cache_control 块数量已调整为 {Count}", count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnforceCacheControlLimit 失败");
            return false;
        }
    }

    /// <summary>
    /// 计算 cache_control 块数量
    /// </summary>
    private int CountCacheControlBlocks(JsonObject requestJson)
    {
        int count = 0;

        // 统计 system 中的块
        if (requestJson.TryGetPropertyValue("system", out var systemNode) &&
            systemNode is JsonArray systemArray)
        {
            foreach (var item in systemArray)
            {
                if (item is JsonObject block)
                {
                    // thinking 块不支持 cache_control，跳过
                    var blockType = block.TryGetPropertyValue("type", out var typeNode)
                        ? typeNode?.GetValue<string>()
                        : null;
                    if (blockType == "thinking") continue;

                    if (block.ContainsKey("cache_control"))
                    {
                        count++;
                    }
                }
            }
        }

        // 统计 messages 中的块
        if (requestJson.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messages)
        {
            foreach (var message in messages)
            {
                if (message is not JsonObject messageObj) continue;

                if (messageObj.TryGetPropertyValue("content", out var contentNode) &&
                    contentNode is JsonArray contentArray)
                {
                    foreach (var item in contentArray)
                    {
                        if (item is JsonObject block)
                        {
                            // thinking 块不支持 cache_control，跳过
                            var blockType = block.TryGetPropertyValue("type", out var typeNode)
                                ? typeNode?.GetValue<string>()
                                : null;
                            if (blockType == "thinking") continue;

                            if (block.ContainsKey("cache_control"))
                            {
                                count++;
                            }
                        }
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 从 messages 中移除一个 cache_control（从头开始）
    /// </summary>
    private bool RemoveCacheControlFromMessages(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("messages", out var messagesNode) ||
            messagesNode is not JsonArray messages)
        {
            return false;
        }

        foreach (var message in messages)
        {
            if (message is not JsonObject messageObj) continue;

            if (messageObj.TryGetPropertyValue("content", out var contentNode) &&
                contentNode is JsonArray contentArray)
            {
                foreach (var item in contentArray)
                {
                    if (item is JsonObject block)
                    {
                        // thinking 块不支持 cache_control，跳过
                        var blockType = block.TryGetPropertyValue("type", out var typeNode)
                            ? typeNode?.GetValue<string>()
                            : null;
                        if (blockType == "thinking") continue;

                        if (block.ContainsKey("cache_control"))
                        {
                            block.Remove("cache_control");
                            logger.LogDebug("从 messages 中移除一个 cache_control");
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 从 system 中移除一个 cache_control（从尾部开始，保护注入的 prompt）
    /// </summary>
    private bool RemoveCacheControlFromSystem(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("system", out var systemNode) ||
            systemNode is not JsonArray systemArray)
        {
            return false;
        }

        // 从尾部开始移除，保护开头注入的 Claude Code prompt
        for (int i = systemArray.Count - 1; i >= 0; i--)
        {
            if (systemArray[i] is JsonObject block)
            {
                // thinking 块不支持 cache_control，跳过
                var blockType = block.TryGetPropertyValue("type", out var typeNode)
                    ? typeNode?.GetValue<string>()
                    : null;
                if (blockType == "thinking") continue;

                if (block.ContainsKey("cache_control"))
                {
                    block.Remove("cache_control");
                    logger.LogDebug("从 system 中移除一个 cache_control（索引 {Index}）", i);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 强制清理所有 thinking 块中的非法 cache_control
    /// </summary>
    private void RemoveCacheControlFromThinkingBlocks(JsonObject requestJson)
    {
        // 清理 system 中的 thinking 块
        if (requestJson.TryGetPropertyValue("system", out var systemNode) &&
            systemNode is JsonArray systemArray)
        {
            foreach (var item in systemArray)
            {
                if (item is JsonObject block)
                {
                    var blockType = block.TryGetPropertyValue("type", out var typeNode)
                        ? typeNode?.GetValue<string>()
                        : null;
                    if (blockType == "thinking" && block.ContainsKey("cache_control"))
                    {
                        block.Remove("cache_control");
                        logger.LogWarning("移除 system 中 thinking 块的非法 cache_control");
                    }
                }
            }
        }

        // 清理 messages 中的 thinking 块
        if (requestJson.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messages)
        {
            for (int msgIdx = 0; msgIdx < messages.Count; msgIdx++)
            {
                if (messages[msgIdx] is not JsonObject messageObj) continue;

                if (messageObj.TryGetPropertyValue("content", out var contentNode) &&
                    contentNode is JsonArray contentArray)
                {
                    for (int contentIdx = 0; contentIdx < contentArray.Count; contentIdx++)
                    {
                        if (contentArray[contentIdx] is JsonObject block)
                        {
                            var blockType = block.TryGetPropertyValue("type", out var typeNode)
                                ? typeNode?.GetValue<string>()
                                : null;
                            if (blockType == "thinking" && block.ContainsKey("cache_control"))
                            {
                                block.Remove("cache_control");
                                logger.LogWarning("移除 messages[{MsgIdx}].content[{ContentIdx}] 中 thinking 块的非法 cache_control",
                                    msgIdx, contentIdx);
                            }
                        }
                    }
                }
            }
        }
    }
}
