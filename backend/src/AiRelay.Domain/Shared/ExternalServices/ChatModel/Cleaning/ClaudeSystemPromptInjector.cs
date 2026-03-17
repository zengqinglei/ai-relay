using System.Text.Json.Nodes;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Claude System Prompt 注入器
/// 负责为 Claude OAuth 账号智能注入 Claude Code 系统提示词
/// </summary>
public class ClaudeSystemPromptInjector
{
    private const string BillingHeader = "x-anthropic-billing-header: cc_version=2.1.74.ee0; cc_entrypoint=cli; cch=00000;";
    public const string ClaudeCodeSystemPrompt = "You are Claude Code, Anthropic's official CLI for Claude.";

    /// <summary>
    /// 创建 billing header block（无 cache_control）
    /// </summary>
    public static JsonObject CreateBillingHeaderBlock()
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = BillingHeader
        };
    }

    /// <summary>
    /// 创建带 cache_control 的 Claude Code system block
    /// </summary>
    public static JsonObject CreateClaudeCodeSystemBlock()
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = ClaudeCodeSystemPrompt,
            ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
        };
    }

    /// <summary>
    /// 创建带 cache_control 的 text content block
    /// </summary>
    public static JsonObject CreateCachedTextBlock(string text)
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = text,
            ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
        };
    }

    /// <summary>
    /// 在 system 开头注入 Claude Code 提示词（仅 OAuth 账号需要）
    /// 处理 null、字符串、数组三种格式
    /// </summary>
    public bool InjectClaudeCodePrompt(JsonObject requestJson)
    {
        try
        {
            // 检查 system 中是否已有 Claude Code 提示词
            if (requestJson.TryGetPropertyValue("system", out var systemNode))
            {
                if (SystemIncludesClaudeCodePrompt(systemNode))
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
                // null: 注入 billing header + Claude Code
                newSystem.Add(billingHeaderBlock);
                newSystem.Add(claudeCodeBlock);
            }
            else if (systemNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var systemString))
            {
                // string: 注入 billing header + Claude Code + 保留原内容
                newSystem.Add(billingHeaderBlock);
                newSystem.Add(claudeCodeBlock);
                if (!string.IsNullOrWhiteSpace(systemString) &&
                    systemString.Trim() != ClaudeCodeSystemPrompt.Trim())
                {
                    newSystem.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = systemString,
                        ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
                    });
                }
            }
            else if (systemNode is JsonArray systemArray)
            {
                // array: 注入 billing header + Claude Code 到开头
                newSystem.Add(billingHeaderBlock);
                newSystem.Add(claudeCodeBlock);

                bool prefixedNext = false;
                foreach (var item in systemArray)
                {
                    if (item is JsonObject block)
                    {
                        // 跳过已存在的 Claude Code 提示词
                        if (block.TryGetPropertyValue("text", out var textNode) &&
                            textNode is JsonValue textValue &&
                            textValue.TryGetValue<string>(out var text) &&
                            text.Trim() == ClaudeCodeSystemPrompt.Trim())
                        {
                            continue;
                        }

                        // 在第一个 text block 前添加前缀
                        if (!prefixedNext &&
                            block.TryGetPropertyValue("type", out var typeNode) &&
                            typeNode?.GetValue<string>() == "text" &&
                            block.TryGetPropertyValue("text", out var blockTextNode) &&
                            blockTextNode is JsonValue blockTextValue &&
                            blockTextValue.TryGetValue<string>(out var blockText) &&
                            !string.IsNullOrWhiteSpace(blockText) &&
                            !blockText.StartsWith(ClaudeCodeSystemPrompt))
                        {
                            block["text"] = $"{ClaudeCodeSystemPrompt}\n\n{blockText}";
                            prefixedNext = true;
                        }
                    }
                    newSystem.Add(item?.DeepClone());
                }
            }

            // 更新 system 字段
            requestJson["system"] = newSystem;
            return true;
        }
        catch
        {
            // 解析失败
            return false;
        }
    }

    /// <summary>
    /// 检查 system 中是否已包含 Claude Code 提示词
    /// </summary>
    private static bool SystemIncludesClaudeCodePrompt(JsonNode? systemNode)
    {
        if (systemNode == null) return false;

        var promptTrimmed = ClaudeCodeSystemPrompt.Trim();

        // string 格式
        if (systemNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var systemString))
        {
            return systemString.Contains(ClaudeCodeSystemPrompt, StringComparison.OrdinalIgnoreCase);
        }

        // array 格式
        if (systemNode is JsonArray systemArray)
        {
            foreach (var item in systemArray)
            {
                if (item is JsonObject block &&
                    block.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    text.Contains(ClaudeCodeSystemPrompt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
