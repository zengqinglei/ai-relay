using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Claude 请求清洗器
/// 用于移除客户端注入的敏感元数据（如计费信息）
/// </summary>
public class ClaudeRequestCleaner(ILogger<ClaudeRequestCleaner> logger)
{
    // 需要过滤的 System Prompt 前缀黑名单
    // OAuth/SetupToken 账号转发时，匹配这些前缀的 system 元素会被移除
    private static readonly string[] SystemBlockPrefixBlacklist =
    {
        "x-anthropic-billing-header", // Claude 平台标准计费头
    };

    /// <summary>
    /// 移除黑名单前缀匹配的 system 元素
    /// </summary>
    public bool FilterSystemBlocksByPrefix(JsonObject requestJson)
    {
        try
        {
            if (!requestJson.TryGetPropertyValue("system", out var systemNode))
            {
                return false;
            }

            // 处理 system 为数组的情况
            if (systemNode is JsonArray systemArray)
            {
                var newSystem = new JsonArray();
                bool modified = false;

                foreach (var item in systemArray)
                {
                    bool shouldKeep = true;
                    if (item is JsonObject block &&
                        block.TryGetPropertyValue("type", out var typeNode) &&
                        typeNode?.GetValue<string>() == "text" &&
                        block.TryGetPropertyValue("text", out var textNode))
                    {
                        var text = textNode?.GetValue<string>();
                        if (!string.IsNullOrEmpty(text))
                        {
                            foreach (var prefix in SystemBlockPrefixBlacklist)
                            {
                                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    shouldKeep = false;
                                    modified = true;
                                    logger.LogDebug("过滤带前缀的 system 块: {Prefix}", prefix);
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldKeep)
                    {
                        newSystem.Add(item?.DeepClone());
                    }
                }

                if (modified)
                {
                    requestJson["system"] = newSystem;
                    return true;
                }
            }
            // 处理 system 为字符串的情况 (通常是一个整体，较少直接命中前缀，但为了保险起见也可以检查)
            else if (systemNode is JsonValue systemValue && systemValue.TryGetValue<string>(out var systemString))
            {
                foreach (var prefix in SystemBlockPrefixBlacklist)
                {
                    if (systemString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // 如果整个 system string 命中了黑名单，直接移除 system 字段或者置空
                        requestJson.Remove("system");
                        logger.LogDebug("过滤带前缀的 system 字符串: {Prefix}", prefix);
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "过滤 system 块前缀失败");
            return false;
        }
    }
}
