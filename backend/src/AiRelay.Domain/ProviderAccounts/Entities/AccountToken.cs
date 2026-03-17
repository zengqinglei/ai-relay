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

    public AccountToken(
        ProviderPlatform platform,
        string name,
        int maxConcurrency,
        string? accessToken = null,
        string? refreshToken = null,
        long? expiresIn = null,
        string? baseUrl = null,
        string? description = null,
        Dictionary<string, string>? extraProperties = null)
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

    public void Update(string? name, string? baseUrl, string? description, int? maxConcurrency, Dictionary<string, string>? extraProperties = null)
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
        StatusDescription = description;

        AddLocalEvent(new AccountCircuitBrokenEvent(Id, lockDuration, description));
    }

    public void MarkAsError(string? description)
    {
        Status = AccountStatus.Error;
        RateLimitDurationSeconds = null;
        StatusDescription = description;
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
