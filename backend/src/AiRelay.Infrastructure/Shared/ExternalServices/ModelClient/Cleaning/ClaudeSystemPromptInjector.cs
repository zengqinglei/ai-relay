using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

/// <summary>
/// Claude System Prompt 注入器
/// 负责为 Claude OAuth 账号智能注入 Claude Code 系统提示词
/// </summary>
public class ClaudeSystemPromptInjector
{
    /// <summary>
    /// 创建 billing header block（无 cache_control）
    /// </summary>
    public static JsonObject CreateBillingHeaderBlock()
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = ClaudeMimicDefaults.BillingHeader
        };
    }

    /// <summary>
    /// 创建无 cache_control 的 Claude Code system block
    /// </summary>
    public static JsonObject CreateClaudeCodeSystemBlock()
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = ClaudeMimicDefaults.ClaudeCodeSystemPrompt
        };
    }

    /// <summary>
    /// 清理已知的第三方伪装提示词（如 OpenCode, OpenClaw 等），防止出现矛盾的身份声明
    /// </summary>
    private static string SanitizeSystemText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // 将已知的第三方身份声明替换为为空字符串（即删除它们）
        // 因为我们在代码其它地方已经强制注入了官方的身份声明，这样能彻底避免重复
        text = text.Replace(ClaudeMimicDefaults.OpenCodeSystemPrompt, "");
        text = text.Replace(ClaudeMimicDefaults.OpenClawSystemPrompt, "");

        return text.Trim();
    }

    /// <summary>
    /// 在 system 开头注入 Claude Code 提示词（仅 OAuth 账号需要）
    /// 处理 null、字符串、数组三种格式
    /// </summary>
    public bool InjectClaudeCodePrompt(JsonObject requestJson)
    {
        try
        {
            var claudeCodeSystemPrompt = ClaudeMimicDefaults.ClaudeCodeSystemPrompt;

            // 检查 system 中是否已有 Claude Code 提示词
            if (requestJson.TryGetPropertyValue("system", out var systemNode))
            {
                if (SystemIncludesClaudeCodePrompt(systemNode, claudeCodeSystemPrompt))
                {
                    return false; // 已存在，不重复注入
                }
            }

            // 构建 billing header 和 Claude Code block
            var billingHeaderBlock = CreateBillingHeaderBlock();
            var claudeCodeBlock = CreateClaudeCodeSystemBlock();

            var newSystem = new JsonArray();

            // 处理不同的 system 格式
            if (systemNode == null)
            {
                newSystem.Add(billingHeaderBlock);
                newSystem.Add(claudeCodeBlock);
            }
            else if (systemNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var systemString))
            {
                systemString = SanitizeSystemText(systemString);

                // 如果清理后该文本块变成空了（比如原本只有一句 OpenCode），直接丢弃该块
                if (string.IsNullOrWhiteSpace(systemString))
                {
                    newSystem.Add(billingHeaderBlock);
                    newSystem.Add(claudeCodeBlock);
                }
                else
                {
                    newSystem.Add(billingHeaderBlock);
                    newSystem.Add(claudeCodeBlock);
                    if (systemString.Trim() != claudeCodeSystemPrompt.Trim())
                    {
                        newSystem.Add(new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = systemString
                        });
                    }
                }
            }
            else if (systemNode is JsonArray systemArray)
            {
                newSystem.Add(billingHeaderBlock);
                newSystem.Add(claudeCodeBlock);

                foreach (var item in systemArray)
                {
                    if (item is JsonObject block)
                    {
                        if (block.TryGetPropertyValue("text", out var textNode) &&
                            textNode is JsonValue textValue &&
                            textValue.TryGetValue<string>(out var text))
                        {
                            text = SanitizeSystemText(text);

                            // 如果清理后该文本块变成空了（比如原本只有一句 OpenCode），直接丢弃该块
                            if (string.IsNullOrWhiteSpace(text))
                                continue;

                            block["text"] = text;

                            // 跳过与注入块重复的内容
                            if (text.Trim() == claudeCodeSystemPrompt.Trim())
                                continue;
                            if (text.TrimStart().StartsWith("x-anthropic-billing-header:", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                    }
                    newSystem.Add(item?.DeepClone());
                }
            }

            requestJson["system"] = newSystem;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查 system 中是否已包含 Claude Code 提示词
    /// </summary>
    private static bool SystemIncludesClaudeCodePrompt(JsonNode? systemNode, string claudeCodeSystemPrompt)
    {
        if (systemNode == null) return false;

        if (systemNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var systemString))
        {
            return systemString.Contains(claudeCodeSystemPrompt, StringComparison.OrdinalIgnoreCase);
        }

        if (systemNode is JsonArray systemArray)
        {
            foreach (var item in systemArray)
            {
                if (item is JsonObject block &&
                    block.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    text.Contains(claudeCodeSystemPrompt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
