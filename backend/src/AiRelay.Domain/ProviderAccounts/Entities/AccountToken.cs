using AiRelay.Domain.ProviderAccounts.Events;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderAccounts.Entities;

public class AccountToken : DeletionAuditedEntity<Guid>
{
    public ProviderPlatform Platform { get; private set; }

    public string Name { get; private set; } = null!;

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public string? BaseUrl { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public AccountStatus Status { get; private set; } = AccountStatus.Normal;

    /// <summary>
    /// 限流时长（秒）
    /// </summary>
    public int? RateLimitDurationSeconds { get; private set; }

    public string? StatusDescription { get; private set; }

    public DateTime? LastStatusUpdateTime { get; private set; }

    /// <summary>
    /// 限流锁定解除时间（用于数据库持久化）
    /// </summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>
    /// 最大并发数（0 表示不限制）
    /// </summary>
    public int MaxConcurrency { get; private set; } = 10;

    /// <summary>
    /// 额外属性（存储平台特定元数据，如 chatgpt_account_id, project_id）
    /// </summary>
    public Dictionary<string, string> ExtraProperties { get; private set; } = new();

    /// <summary>
    /// 模型白名单（限制可接受的模型，为空时支持全部）
    /// </summary>
    public List<string>? ModelWhites { get; private set; }

    /// <summary>
    /// 模型映射规则（转换模型名称，支持通配符）
    /// </summary>
    public Dictionary<string, string>? ModelMapping { get; private set; }

    /// <summary>
    /// 是否允许伪装为官方客户端
    /// </summary>
    public bool AllowOfficialClientMimic { get; private set; }

    /// <summary>
    /// 流健康度检查 (主动拦截没有输出或是带有错误抛出的残缺数据流)
    /// </summary>
    public bool IsCheckStreamHealth { get; private set; }

    // ── 计费统计字段 ──────────────────────────────────────────────────────────

    /// <summary>今日调用次数（UTC 自然日，跨日自动归零）</summary>
    public long UsageToday { get => StatsDate?.Date == DateTime.UtcNow.Date ? field : 0; private set; }

    /// <summary>累计调用次数</summary>
    public long UsageTotal { get; private set; }

    /// <summary>今日消耗额度（USD）</summary>
    public decimal CostToday { get => StatsDate?.Date == DateTime.UtcNow.Date ? field : 0; private set; }

    /// <summary>累计消耗额度（USD）</summary>
    public decimal CostTotal { get; private set; }

    /// <summary>今日消耗 Token 数</summary>
    public long TokensToday { get => StatsDate?.Date == DateTime.UtcNow.Date ? field : 0; private set; }

    /// <summary>累计消耗 Token 数</summary>
    public long TokensTotal { get; private set; }

    /// <summary>今日成功次数</summary>
    public long SuccessToday { get => StatsDate?.Date == DateTime.UtcNow.Date ? field : 0; private set; }

    /// <summary>累计成功次数</summary>
    public long SuccessTotal { get; private set; }

    /// <summary>今日统计基准日期（UTC），用于跨日自动重置</summary>
    public DateTime? StatsDate { get; private set; }

    /// <summary>
    /// 累加调用次数统计（每次 attempt 调用，含失败）
    /// </summary>
    public void AccumulateCallStats(bool isSuccess)
    {
        var today = DateTime.UtcNow.Date;

        UsageTotal++;
        if (isSuccess) SuccessTotal++;

        if (StatsDate?.Date != today)
        {
            UsageToday = 1;
            TokensToday = 0;
            CostToday = 0;
            SuccessToday = isSuccess ? 1 : 0;
            StatsDate = today;
        }
        else
        {
            UsageToday++;
            if (isSuccess) SuccessToday++;
        }
    }

    /// <summary>
    /// 累加 Token/费用统计（请求完成后调用）
    /// </summary>
    public void AccumulateCostStats(long tokens, decimal cost)
    {
        TokensTotal += tokens;
        CostTotal += cost;

        if (StatsDate?.Date == DateTime.UtcNow.Date)
        {
            TokensToday += tokens;
            CostToday += cost;
        }
    }

    public AccountToken(
        ProviderPlatform platform,
        string name,
        int maxConcurrency,
        string? accessToken = null,
        string? refreshToken = null,
        long? expiresIn = null,
        string? baseUrl = null,
        string? description = null,
        Dictionary<string, string>? extraProperties = null,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null,
        bool allowOfficialClientMimic = false,
        bool isCheckStreamHealth = false)
    {
        Id = Guid.CreateVersion7();
        Platform = platform;
        Name = name;
        MaxConcurrency = maxConcurrency;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        BaseUrl = baseUrl;
        Description = description;
        if (extraProperties != null)
        {
            ExtraProperties = extraProperties;
        }
        ModelWhites = modelWhites;
        ModelMapping = modelMapping;
        AllowOfficialClientMimic = allowOfficialClientMimic;
        IsCheckStreamHealth = isCheckStreamHealth;

        if (expiresIn.HasValue)
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn.Value);
        }
    }

    private AccountToken()
    {
        // For ORM
    }

    public void Disable() => IsActive = false;

    public void Enable() => IsActive = true;

    public void Update(string? name, string? baseUrl, string? description, int? maxConcurrency, Dictionary<string, string>? extraProperties = null,
        List<string>? modelWhites = null, Dictionary<string, string>? modelMapping = null, bool clearModelWhites = false, bool clearModelMapping = false,
        bool? allowOfficialClientMimic = null, bool? isCheckStreamHealth = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }

        if (baseUrl != null)
        {
            BaseUrl = baseUrl;
        }

        if (description != null)
        {
            Description = description;
        }

        if (maxConcurrency.HasValue)
        {
            MaxConcurrency = maxConcurrency.Value;
        }

        if (extraProperties != null)
        {
            ExtraProperties = extraProperties;
        }

        if (clearModelWhites)
            ModelWhites = null;
        else if (modelWhites != null)
            ModelWhites = modelWhites;

        if (clearModelMapping)
            ModelMapping = null;
        else if (modelMapping != null)
            ModelMapping = modelMapping;

        if (allowOfficialClientMimic.HasValue)
        {
            AllowOfficialClientMimic = allowOfficialClientMimic.Value;
        }

        if (isCheckStreamHealth.HasValue)
        {
            IsCheckStreamHealth = isCheckStreamHealth.Value;
        }
    }

    public void UpdateTokens(string accessToken, string? refreshToken, long? expiresIn)
    {
        AccessToken = accessToken;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            RefreshToken = refreshToken;
        }

        if (expiresIn.HasValue)
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn.Value);
        }
    }

    public void UpdateRefreshToken(string refreshToken)
    {
        RefreshToken = refreshToken;
    }

    /// <summary>
    /// 从另一个实例复制 token 相关字段（用于分布式锁 double-check 后同步最新状态）
    /// </summary>
    public void CopyTokenFrom(AccountToken source)
    {
        AccessToken = source.AccessToken;
        RefreshToken = source.RefreshToken;
        ExpiresAt = source.ExpiresAt;
    }

    public bool IsNeedRefreshToken()
    {
        if (string.IsNullOrEmpty(RefreshToken))
        {
            return false;
        }

        if (string.IsNullOrEmpty(AccessToken))
        {
            return true;
        }

        if (!ExpiresAt.HasValue)
        {
            return true;
        }

        return DateTime.UtcNow >= ExpiresAt.Value.AddMinutes(-3);
    }

    public double? GetTokenRemainingMinutes()
    {
        return ExpiresAt.HasValue
            ? (ExpiresAt.Value - DateTime.UtcNow).TotalMinutes
            : null;
    }

    public void MarkAsRateLimited(TimeSpan lockDuration, string description)
    {
        Status = AccountStatus.RateLimited;
        RateLimitDurationSeconds = (int)lockDuration.TotalSeconds;
        LockedUntil = DateTime.UtcNow.AddSeconds((int)lockDuration.TotalSeconds);
        StatusDescription = description?.Length > 512 ? description[..512] : description;

        AddLocalEvent(new AccountCircuitBrokenEvent(Id, lockDuration, StatusDescription));
    }

    public void MarkAsError(string? description)
    {
        Status = AccountStatus.Error;
        RateLimitDurationSeconds = null;
        StatusDescription = description?.Length > 512 ? description.Substring(0, 512) : description;
    }

    public bool ResetStatus()
    {
        if (Status == AccountStatus.Normal)
            return false;

        Status = AccountStatus.Normal;
        RateLimitDurationSeconds = null;
        LockedUntil = null;
        StatusDescription = null;
        AddLocalEvent(new AccountRecoveredEvent(Id));
        return true;
    }

    /// <summary>
    /// 获取当前有效状态（限流过期时返回 Normal）
    /// </summary>
    public AccountStatus GetEffectiveStatus()
    {
        if (IsRateLimitExpired())
        {
            return AccountStatus.Normal;
        }
        return Status;
    }

    /// <summary>
    /// 判断限流是否已过期
    /// </summary>
    public bool IsRateLimitExpired()
    {
        return Status == AccountStatus.RateLimited && LockedUntil.HasValue && LockedUntil.Value <= DateTime.UtcNow;
    }

    public bool IsAvailable()
    {
        if (!IsActive) return false;

        var effectiveStatus = GetEffectiveStatus();
        if (effectiveStatus == AccountStatus.Normal) return true;

        return IsRateLimitExpired();
    }
}
