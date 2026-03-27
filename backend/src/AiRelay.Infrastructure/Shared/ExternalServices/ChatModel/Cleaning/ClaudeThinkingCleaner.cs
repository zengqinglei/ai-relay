using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Claude Thinking 块清洗器
/// 负责处理 Claude 请求中的 thinking 块降级策略（两阶段降级）
/// </summary>
public class ClaudeThinkingCleaner(ILogger<ClaudeThinkingCleaner> logger)
{
    /// <summary>
    /// 第一阶段降级：转换 thinking/redacted_thinking 块
    /// - thinking → text（保留思考内容）
    /// - redacted_thinking → 删除（无法转换加密内容）
    /// - 移除顶层 thinking 配置
    /// </summary>
    public bool FilterThinkingBlocks(JsonObject requestJson)
    {
        try
        {
            bool modified = false;

            // 1. 移除顶层 thinking 字段
            if (requestJson.ContainsKey("thinking"))
            {
                requestJson.Remove("thinking");
                logger.LogDebug("移除顶层 thinking 配置");
                modified = true;
            }

            // 2. 转换消息中的 thinking 块
            if (requestJson.TryGetPropertyValue("messages", out var messagesNode) &&
                messagesNode is JsonArray messages)
            {
                foreach (var message in messages)
                {
                    if (message is not JsonObject messageObj) continue;

                    if (messageObj.TryGetPropertyValue("content", out var contentNode))
                    {
                        if (contentNode is JsonArray contentArray)
                        {
                            var newContent = new JsonArray();
                            bool contentModified = false;

                            foreach (var block in contentArray)
                            {
                                if (block is not JsonObject blockObj)
                                {
                                    newContent.Add(block?.DeepClone());
                                    continue;
                                }

                                var blockType = blockObj.TryGetPropertyValue("type", out var typeNode)
                                    ? typeNode?.GetValue<string>()
                                    : null;

                                if (blockType == "thinking")
                                {
                                    // 转换为 text 块
                                    var thinkingText = blockObj.TryGetPropertyValue("thinking", out var thinkingNode)
                                        ? thinkingNode?.GetValue<string>() ?? ""
                                        : "";

                                    if (!string.IsNullOrWhiteSpace(thinkingText))
                                    {
                                        newContent.Add(new JsonObject
                                        {
                                            ["type"] = "text",
                                            ["text"] = thinkingText
                                        });
                                        logger.LogDebug("转换 thinking 块为 text 块");
                                        contentModified = true;
                                    }
                                }
                                else if (blockType == "redacted_thinking")
                                {
                                    // 删除 redacted_thinking 块（无法转换加密内容）
                                    logger.LogDebug("删除 redacted_thinking 块");
                                    contentModified = true;
                                }
                                else
                                {
                                    // 保留其他块
                                    newContent.Add(block.DeepClone());
                                }
                            }

                            // 确保消息内容不为空
                            if (contentModified && newContent.Count > 0)
                            {
                                messageObj["content"] = newContent;
                                modified = true;
                            }
                        }
                    }
                }
            }

            return modified;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FilterThinkingBlocks 失败");
            return false;
        }
    }

    /// <summary>
    /// 第二阶段降级：在第一阶段基础上，转换 tool_use/tool_result 块
    /// 用于签名错误后的深度降级
    /// - tool_use → text（格式化为文本描述）
    /// - tool_result → text（格式化为文本描述）
    /// </summary>
    public bool FilterSignatureSensitiveBlocks(JsonObject requestJson)
    {
        try
        {
            // 先执行第一阶段降级
            bool modified = FilterThinkingBlocks(requestJson);

            // 转换消息中的 tool_use/tool_result 块
            if (requestJson.TryGetPropertyValue("messages", out var messagesNode) &&
                messagesNode is JsonArray messages)
            {
                foreach (var message in messages)
                {
                    if (message is not JsonObject messageObj) continue;

                    if (messageObj.TryGetPropertyValue("content", out var contentNode))
                    {
                        if (contentNode is JsonArray contentArray)
                        {
                            var newContent = new JsonArray();
                            bool contentModified = false;

                            foreach (var block in contentArray)
                            {
                                if (block is not JsonObject blockObj)
                                {
                                    newContent.Add(block?.DeepClone());
                                    continue;
                                }

                                var blockType = blockObj.TryGetPropertyValue("type", out var typeNode)
                                    ? typeNode?.GetValue<string>()
                                    : null;

                                if (blockType == "tool_use")
                                {
                                    // 转换为 text 块
                                    var toolName = blockObj.TryGetPropertyValue("name", out var nameNode)
                                        ? nameNode?.GetValue<string>() ?? "unknown"
                                        : "unknown";
                                    var toolInput = blockObj.TryGetPropertyValue("input", out var inputNode)
                                        ? inputNode?.ToJsonString() ?? "{}"
                                        : "{}";

                                    var textRepresentation = $"[Tool Use: {toolName}]\nInput: {toolInput}";

                                    newContent.Add(new JsonObject
                                    {
                                        ["type"] = "text",
                                        ["text"] = textRepresentation
                                    });
                                    logger.LogDebug("转换 tool_use 块为 text 块: {ToolName}", toolName);
                                    contentModified = true;
                                }
                                else if (blockType == "tool_result")
                                {
                                    // 转换为 text 块
                                    var toolUseId = blockObj.TryGetPropertyValue("tool_use_id", out var idNode)
                                        ? idNode?.GetValue<string>() ?? "unknown"
                                        : "unknown";
                                    var resultContent = blockObj.TryGetPropertyValue("content", out var resultNode)
                                        ? resultNode?.ToJsonString() ?? ""
                                        : "";

                                    var textRepresentation = $"[Tool Result: {toolUseId}]\n{resultContent}";

                                    newContent.Add(new JsonObject
                                    {
                                        ["type"] = "text",
                                        ["text"] = textRepresentation
                                    });
                                    logger.LogDebug("转换 tool_result 块为 text 块: {ToolUseId}", toolUseId);
                                    contentModified = true;
                                }
                                else
                                {
                                    // 保留其他块
                                    newContent.Add(block.DeepClone());
                                }
                            }

                            // 确保消息内容不为空
                            if (contentModified && newContent.Count > 0)
                            {
                                messageObj["content"] = newContent;
                                modified = true;
                            }
                        }
                    }
                }
            }

            return modified;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FilterSignatureSensitiveBlocks 失败");
            return false;
        }
    }

    /// <summary>
    /// 检测是否为 thinking block 签名错误（需要降级重试）
    /// </summary>
    public static bool IsThinkingBlockSignatureError(string responseBody)
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
