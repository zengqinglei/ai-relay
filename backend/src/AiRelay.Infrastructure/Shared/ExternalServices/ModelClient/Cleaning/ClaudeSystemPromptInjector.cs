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
    /// 在 system 开头注入 Claude Code 提示词（仅 OAuth 账号需要）
    /// 并将原有的用户 system 消息迁移至 messages 数组的首个 user/assistant 对中以实现最高仿真度
    /// </summary>
    public bool InjectClaudeCodePrompt(JsonObject requestJson)
    {
        try
        {
            var claudeCodeSystemPrompt = ClaudeMimicDefaults.ClaudeCodeSystemPrompt;

            // 1. 检查是否已经注入过官方提示词，防止重复
            if (requestJson.TryGetPropertyValue("system", out var existingSystemNode))
            {
                if (SystemIncludesClaudeCodePrompt(existingSystemNode, claudeCodeSystemPrompt))
                {
                    return false;
                }
            }

            // 2. 仿真度优化：将原始 system 内容迁移至 messages 数组的首个 user 消息中
            // 官方客户端通常不在 system 中放动态业务逻辑，而是放在 user message 的头部
            MigrateSystemToUserMessage(requestJson);

            // 3. 构造纯净的官方提示词（Billing Header + Claude Code Identity）
            var newSystem = new JsonArray
            {
                CreateBillingHeaderBlock(),
                CreateClaudeCodeSystemBlock()
            };

            requestJson["system"] = newSystem;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将 system 字段内容作为 user/assistant 消息对迁移至 messages 数组最前面。
    /// 这样可保持 Claude API 要求的角色交替顺序（User -> Assistant -> User...）。
    /// </summary>
    private void MigrateSystemToUserMessage(JsonObject body)
    {
        if (!body.TryGetPropertyValue("system", out var systemNode) || systemNode == null)
            return;

        var messages = body["messages"]?.AsArray();
        if (messages == null) return;

        // 1. 构造 user 消息块（原始系统指令）
        var userMessage = new JsonObject
        {
            ["role"] = "user"
        };

        if (systemNode is JsonValue)
        {
            userMessage["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = "[System Instructions]\n" + systemNode.GetValue<string>() } };
        }
        else if (systemNode is JsonArray sysArray)
        {
            var contentArray = sysArray.DeepClone().AsArray();
            // 在第一个文本块前添加标记
            if (contentArray.Count > 0 &&
                contentArray[0] is JsonObject firstBlock &&
                firstBlock.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text))
            {
                firstBlock["text"] = "[System Instructions]\n" + text;
            }
            userMessage["content"] = contentArray;
        }
        else
        {
            return;
        }

        // 2. 构造 assistant 确认块（保持角色交替）
        var assistantMessage = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = "Understood. I will follow these instructions."
                }
            }
        };

        // 3. 依次插入最前面
        messages.Insert(0, assistantMessage);
        messages.Insert(0, userMessage);

        // 4. 移除原 system
        body.Remove("system");
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

