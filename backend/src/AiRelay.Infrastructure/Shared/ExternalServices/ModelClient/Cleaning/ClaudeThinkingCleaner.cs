using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

/// <summary>
/// Claude Thinking 块清洗器
/// 负责处理 Claude 请求中的 thinking 块降级策略（两阶段降级）
/// </summary>
public class ClaudeThinkingCleaner(ILogger<ClaudeThinkingCleaner> logger)
{
    /// <summary>
    /// 第一阶段降级：处理 thinking/redacted_thinking 和签名
    /// </summary>
    public bool FilterThinkingBlocks(JsonObject requestJson)
    {
        try
        {
            return DeepCleanClaudePayload(requestJson, 1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClaudeThinkingCleaner: FilterThinkingBlocks 失败");
            return false;
        }
    }

    /// <summary>
    /// 第二阶段降级：在第一阶段基础上，转换工具调用为文本描述
    /// </summary>
    public bool FilterSignatureSensitiveBlocks(JsonObject requestJson)
    {
        try
        {
            return DeepCleanClaudePayload(requestJson, 2);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClaudeThinkingCleaner: FilterSignatureSensitiveBlocks 失败");
            return false;
        }
    }

    /// <summary>
    /// 核心逻辑：深度递归清理 Claude 请求体
    /// </summary>
    private bool DeepCleanClaudePayload(JsonObject requestJson, int level)
    {
        bool modified = false;

        // 1. 移除顶层配置
        if (requestJson.Remove("thinking"))
        {
            logger.LogDebug("已移除顶层 thinking 配置");
            modified = true;
        }

        // 2. 处理消息数组
        if (requestJson.TryGetPropertyValue("messages", out var messagesNode) && messagesNode is JsonArray messages)
        {
            foreach (var message in messages)
            {
                if (message is not JsonObject messageObj) continue;
                if (DeepCleanContentArray(messageObj, level)) modified = true;
            }
        }

        return modified;
    }

    private bool DeepCleanContentArray(JsonObject messageObj, int level)
    {
        if (!messageObj.TryGetPropertyValue("content", out var contentNode)) return false;

        // 处理字符串格式的内容（直接跳过，没啥好清理的）
        if (contentNode is not JsonArray contentArray) return false;

        bool contentModified = false;
        var finalContent = new JsonArray();
        JsonObject? primaryTextBlock = null;

        // 第一次遍历：识别或确保有一个主文本块，用于合并内容
        foreach (var block in contentArray)
        {
            if (block is JsonObject b && b.TryGetPropertyValue("type", out var t) && t?.GetValue<string>() == "text")
            {
                primaryTextBlock = b.DeepClone().AsObject();
                break;
            }
        }

        foreach (var block in contentArray)
        {
            if (block is not JsonObject blockObj)
            {
                finalContent.Add(block?.DeepClone());
                continue;
            }

            var type = blockObj["type"]?.GetValue<string>();

            // 1. 处理签名（全级别清理）
            if (blockObj.Remove("signature"))
            {
                logger.LogDebug("已从 content 块中移除残留的 signature 字段");
                contentModified = true;
            }

            // 2. 处理思维块
            if (type == "thinking")
            {
                var thoughtText = blockObj["thinking"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(thoughtText))
                {
                    logger.LogDebug("正在合并思维块内容至主文本块");
                    MergeIntoTextBlock(ref primaryTextBlock, $"[Original Thought]:\n{thoughtText}");
                }
                contentModified = true;
                continue; // 丢弃原块
            }

            if (type == "redacted_thinking")
            {
                logger.LogDebug("已丢弃无法降级的 redacted_thinking 块");
                contentModified = true;
                continue; // 直接丢弃加密思维，无法降级
            }

            // 3. 处理工具块 (Level 2+)
            if (level >= 2)
            {
                if (type == "tool_use")
                {
                    var name = blockObj["name"]?.GetValue<string>() ?? "unknown";
                    var input = blockObj["input"]?.ToJsonString() ?? "{}";
                    logger.LogDebug("正在降级工具调用块: {ToolName}", name);
                    MergeIntoTextBlock(ref primaryTextBlock, $"[Tool Use: {name}]\nInput: {input}");
                    contentModified = true;
                    continue;
                }
                if (type == "tool_result")
                {
                    var id = blockObj["tool_use_id"]?.GetValue<string>() ?? "unknown";
                    var output = blockObj["content"]?.ToJsonString() ?? "";
                    logger.LogDebug("正在降级工具结果块: {ToolUseId}", id);
                    MergeIntoTextBlock(ref primaryTextBlock, $"[Tool Result: {id}]\n{output}");
                    contentModified = true;
                    continue;
                }
            }

            // 文本块：首个作为主块，后续块合并内容以避免丢失，统一在末尾插入
            if (type == "text")
            {
                if (primaryTextBlock == null)
                {
                    primaryTextBlock = blockObj.DeepClone().AsObject();
                }
                else
                {
                    var extraText = blockObj["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(extraText))
                    {
                        MergeIntoTextBlock(ref primaryTextBlock, extraText);
                        contentModified = true;
                    }
                }
                continue;
            }

            finalContent.Add(blockObj.DeepClone());
        }

        // 最后组装：将唯一的合并文本块放在最前面（或原位），确保不违反 API 协议
        if (primaryTextBlock != null)
        {
            finalContent.Insert(0, primaryTextBlock);
            // 这里不一定非要 modified = true，除非发生了内容变化
        }

        if (contentModified || finalContent.Count != contentArray.Count)
        {
            messageObj["content"] = finalContent;
            return true;
        }

        return false;
    }

    private void MergeIntoTextBlock(ref JsonObject? target, string textToAppend)
    {
        if (target == null)
        {
            target = new JsonObject
            {
                ["type"] = "text",
                ["text"] = textToAppend
            };
        }
        else
        {
            var current = target["text"]?.GetValue<string>() ?? "";
            target["text"] = string.IsNullOrWhiteSpace(current) 
                ? textToAppend 
                : $"{current}\n\n{textToAppend}";
        }
    }

    /// <summary>
    /// 检测是否为 thinking block 签名错误（需要降级重试）
    /// </summary>
    public static bool IsThinkingBlockSignatureError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;

        var lowerBody = responseBody.ToLowerInvariant();

        // 检测多种 thinking 相关错误模式
        return lowerBody.Contains("signature") ||
               lowerBody.Contains("expected thinking or redacted_thinking, but found text") ||
               lowerBody.Contains("thinking") && lowerBody.Contains("cannot be modified") ||
               lowerBody.Contains("non-empty content") ||
               lowerBody.Contains("empty content");
    }
}
