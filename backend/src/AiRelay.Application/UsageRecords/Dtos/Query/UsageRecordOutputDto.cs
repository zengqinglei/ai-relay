using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 使用记录列表输出（含最新一次 Attempt 的关键字段）
/// </summary>
public class UsageRecordOutputDto
{
    /// <summary>记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>请求时间</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>API KEY 名称</summary>
    public string ApiKeyName { get; set; } = string.Empty;

    /// <summary>平台类型</summary>
    public ProviderPlatform Platform { get; set; }

    /// <summary>下游请求模型ID</summary>
    public string? DownModelId { get; set; }

    /// <summary>请求路径</summary>
    public string DownRequestUrl { get; set; } = string.Empty;

    /// <summary>请求方法</summary>
    public string DownRequestMethod { get; set; } = "POST";

    /// <summary>是否流式</summary>
    public bool IsStreaming { get; set; }

    /// <summary>User Agent</summary>
    public string DownUserAgent { get; set; } = string.Empty;

    /// <summary>输入 Token 数</summary>
    public int? InputTokens { get; set; }

    /// <summary>输出 Token 数</summary>
    public int? OutputTokens { get; set; }

    /// <summary>缓存读取 Token 数</summary>
    public int? CacheReadTokens { get; set; }

    /// <summary>缓存创建 Token 数</summary>
    public int? CacheCreationTokens { get; set; }

    /// <summary>请求 IP</summary>
    public string DownClientIp { get; set; } = string.Empty;

    /// <summary>消耗金额</summary>
    public decimal? FinalCost { get; set; }

    /// <summary>状态</summary>
    public UsageStatus Status { get; set; }

    /// <summary>耗时（毫秒）</summary>
    public long? DurationMs { get; set; }

    /// <summary>状态描述 (错误信息)</summary>
    public string? StatusDescription { get; set; }

    /// <summary>总尝试次数</summary>
    public int AttemptCount { get; set; }

    // --- 来自最新一次 Attempt ---

    /// <summary>分组名称（取最新 Attempt）</summary>
    public string? ProviderGroupName { get; set; }

    /// <summary>账号名称（取最新 Attempt）</summary>
    public string? AccountTokenName { get; set; }

    /// <summary>上游模型ID（取最新 Attempt）</summary>
    public string? UpModelId { get; set; }

    /// <summary>上游状态码（取最新 Attempt）</summary>
    public int? UpStatusCode { get; set; }

    /// <summary>返回给下游客户端的 HTTP 状态码</summary>
    public int? DownStatusCode { get; set; }
}
