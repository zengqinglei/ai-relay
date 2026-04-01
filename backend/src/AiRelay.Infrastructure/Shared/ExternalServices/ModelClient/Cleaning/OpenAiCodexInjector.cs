using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

/// <summary>
/// OpenAI Codex 指令注入器
/// 负责为 OpenAI Codex 模式请求注入系统指令
/// </summary>
public class OpenAiCodexInjector(ILogger<OpenAiCodexInjector> logger)
{
    private const string CodexInstructions = @"You are a coding agent running in the Codex CLI, a terminal-based coding assistant. Codex CLI is an open source project led by OpenAI. You are expected to be precise, safe, and helpful.

Your capabilities:
- Receive user prompts and other context provided by the harness
- Communicate with the user by streaming thinking & responses
- Emit function calls to run terminal commands and apply patches
- Access to the user's file system and terminal environment

Guidelines:
- Be concise and direct in your responses
- Prioritize actionable information over general explanations
- Use function calls to interact with the environment
- Explain your reasoning when making recommendations";

    // Codex CLI User-Agent 前缀
    private static readonly string[] CodexCLIUserAgentPrefixes = { "codex_vscode/", "codex_cli_rs/" };

    /// <summary>
    /// 注入 Codex Instructions（仅 Codex 模式）
    /// </summary>
    public void InjectCodexInstructions(JsonObject requestJson, string? userAgent)
    {
        bool isCodexCLI = IsCodexCLIRequest(userAgent);

        if (isCodexCLI)
        {
            // Codex CLI 请求：仅在 instructions 为空时补充
            if (!IsInstructionsEmpty(requestJson))
            {
                return; // 已有有效 instructions，不修改
            }

            requestJson["instructions"] = CodexInstructions;
            logger.LogDebug("补充 Codex CLI instructions（原 instructions 为空）");
        }
        else
        {
            // 非 Codex CLI 请求：优先覆盖
            var existingInstructions = requestJson.TryGetPropertyValue("instructions", out var instrNode) &&
                                       instrNode is JsonValue instrValue &&
                                       instrValue.TryGetValue<string>(out var instrStr)
                ? instrStr?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(existingInstructions) ||
                existingInstructions != CodexInstructions.Trim())
            {
                requestJson["instructions"] = CodexInstructions;
            }
        }
    }

    /// <summary>
    /// 检查是否为 Codex CLI 请求
    /// </summary>
    public static bool IsCodexCLIRequest(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return false;

        foreach (var prefix in CodexCLIUserAgentPrefixes)
        {
            if (userAgent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查 instructions 是否为空
    /// </summary>
    private static bool IsInstructionsEmpty(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("instructions", out var instrNode))
        {
            return true; // 字段不存在
        }

        if (instrNode is JsonValue instrValue &&
            instrValue.TryGetValue<string>(out var instrStr))
        {
            return string.IsNullOrWhiteSpace(instrStr);
        }

        return true; // 非字符串类型视为空
    }
}
