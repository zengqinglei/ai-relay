using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

public class OpenAiHeaderProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "accept-language",
        "content-type",
        "conversation_id",
        "user-agent",
        "originator",
        "session_id",
        "x-codex-turn-state",
        "x-codex-turn-metadata",
        "openai-beta"
    };


    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单透传
        foreach (var kvp in down.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                up.Headers[kvp.Key] = kvp.Value;
        }

        if (options.Platform == ProviderPlatform.OPENAI_OAUTH)
        {
            // 覆盖认证信息
            up.Headers["authorization"] = $"Bearer {options.Credential}";
        }
        else
        {
            // 清理可能存在的认证头
            up.Headers.Remove("x-api-key");
            up.Headers.Remove("x-goog-api-key");
            up.Headers.Remove("cookie");

            up.Headers["authorization"] = $"Bearer {options.Credential}";
        }

        // 伪装官方客户端
        if (options.ShouldMimicOfficialClient)
            CoverCliHeaders(up.Headers, down, options);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 覆盖官方 CLI Headers：官方客户端透传已有值，非官方客户端补充缺失的默认值
    /// </summary>
    private static void CoverCliHeaders(Dictionary<string, string> headers, DownRequestContext down, ChatModelConnectionOptions options)
    {
        bool isOfficialClient = OpenAiCodexInjector.IsCodexCLIRequest(down.GetUserAgent());
        if (isOfficialClient)
            return; // 官方客户端：身份标识透传，不补充默认值

        if (!headers.ContainsKey("OpenAI-Beta"))
            headers["OpenAI-Beta"] = "responses=experimental";
        if (!headers.ContainsKey("content-type"))
            headers["accept"] = "application/json";
        if (!headers.ContainsKey("accept"))
            headers["accept"] = "text/event-stream";
        var sessionId = down.StickySessionId ?? Guid.NewGuid().ToString("D");
        if (!headers.ContainsKey("conversation_id"))
            headers["conversation_id"] = sessionId;
        if (!headers.ContainsKey("session_id"))
            headers["session_id"] = sessionId;
        // chatgpt_account_id 始终注入（账号绑定信息，非伪装字段）
        if (options.Platform == ProviderPlatform.OPENAI_OAUTH
            && options.ExtraProperties.TryGetValue("chatgpt_account_id", out var accountId)
            && !string.IsNullOrWhiteSpace(accountId))
        {
            headers["chatgpt-account-id"] = accountId;
        }

        if (!isOfficialClient)
        {
            headers["originator"] = "codex_cli_rs";
            headers["originator"] = "codex_cli_rs/0.116.0 (Windows 10.0.26100; x86_64) WindowsTerminal";
        }
    }
}
