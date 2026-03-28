using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Constants;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude OAuth 模式 Header 处理器
/// </summary>
public class ClaudeHeaderProcessor(
    ChatModelConnectionOptions options,
    IClaudeCodeClientDetector clientDetector) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单透传
        foreach (var kvp in down.Headers)
        {
            if (ClaudeMimicDefaults.Headers.TryGetValue(kvp.Key, out var config) &&
                config.AllowPassthrough &&
                !string.IsNullOrEmpty(kvp.Value))
            {
                up.Headers[kvp.Key] = kvp.Value;
            }
        }

        if (options.Platform == ProviderPlatform.CLAUDE_OAUTH)
        {
            // 覆盖认证信息
            up.Headers["Authorization"] = $"Bearer {options.Credential}";
        }
        else
        {
            // 移除 OAuth 相关 headers，覆盖认证信息
            up.Headers.Remove("Authorization");
            up.Headers.Remove("cookie");
            up.Headers["x-api-key"] = options.Credential;
        }

        // 伪装官方客户端
        if (options.ShouldMimicOfficialClient)
        {
            bool isOfficialClient = clientDetector.IsClaudeCodeClient(down, down.BodyJsonNode as JsonObject);
            bool isHaikuModel = !string.IsNullOrEmpty(up.MappedModelId) &&
                                up.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);
            CoverCliHeaders(up.Headers, isOfficialClient, isHaikuModel);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 覆盖官方 CLI Headers：官方客户端透传已有值，非官方客户端补充缺失的默认值或强制覆盖
    /// </summary>
    private static void CoverCliHeaders(Dictionary<string, string> headers, bool isOfficialClient, bool isHaikuModel)
    {
        if (isOfficialClient)
            return; // 官方客户端：身份标识透传，不补充默认值

        // 非官方客户端：遍历配置
        foreach (var (key, (_, defaultValue, forceOverride)) in ClaudeMimicDefaults.Headers)
        {
            if (defaultValue == null)
                continue;

            if (forceOverride || !headers.ContainsKey(key))
                headers[key] = defaultValue;
        }

        // anthropic-beta 根据模型动态设置（强制覆盖）
        headers["anthropic-beta"] = isHaikuModel ? ClaudeMimicDefaults.AnthropicBetaHaiku : ClaudeMimicDefaults.AnthropicBeta;
    }
}
