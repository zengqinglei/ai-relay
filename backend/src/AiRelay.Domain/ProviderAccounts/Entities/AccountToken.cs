using AiRelay.Domain.ProviderAccounts.Events;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderAccounts.Entities;

public class AccountToken : DeletionAuditedEntity<Guid>
{
    public Provider Provider { get; private set; }

    public AuthMethod AuthMethod { get; private set; }

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
    /// 调度优先级（值越小优先级越高）
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// 调度权重（同优先级内的分配因子，1-100）
    /// </summary>
    public int Weight { get; private set; } = 50;

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
    /// 限流维度（按账户 / 按模型）
    /// </summary>
    public RateLimitScope RateLimitScope { get; private set; } = RateLimitScope.Account;

    /// <summary>
    /// 当前处于模型级限流中的模型状态列表
    /// </summary>
    public List<LimitedModelState>? LimitedModels { get; private set; }

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
        Provider provider,
        AuthMethod authMethod,
        string name,
        int maxConcurrency,
        int priority = 1,
        int weight = 50,
        string? accessToken = null,
        string? refreshToken = null,
        long? expiresIn = null,
        string? baseUrl = null,
        string? description = null,
        Dictionary<string, string>? extraProperties = null,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null,
        RateLimitScope rateLimitScope = RateLimitScope.Account,
        bool allowOfficialClientMimic = false,
        bool isCheckStreamHealth = false)
    {
        Id = Guid.CreateVersion7();
        Provider = provider;
        AuthMethod = authMethod;
        Name = name;
        MaxConcurrency = maxConcurrency;
        Priority = priority;
        Weight = weight;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
        Description = description;
        if (extraProperties != null)
        {
            ExtraProperties = extraProperties;
        }
        ModelWhites = modelWhites;
        ModelMapping = modelMapping;
        RateLimitScope = rateLimitScope;
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

    public void Update(
        string? name,
        string? baseUrl,
        string? description,
        int? maxConcurrency,
        Dictionary<string, string>? extraProperties = null,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null,
        bool clearModelWhites = false,
        bool clearModelMapping = false,
        RateLimitScope? rateLimitScope = null,
        bool? allowOfficialClientMimic = null,
        bool? isCheckStreamHealth = null,
        int? priority = null,
        int? weight = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }

        if (baseUrl != null)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
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

        if (rateLimitScope.HasValue)
        {
            UpdateRateLimitScope(rateLimitScope.Value);
        }

        if (allowOfficialClientMimic.HasValue)
        {
            AllowOfficialClientMimic = allowOfficialClientMimic.Value;
        }

        if (isCheckStreamHealth.HasValue)
        {
            IsCheckStreamHealth = isCheckStreamHealth.Value;
        }

        if (priority.HasValue || weight.HasValue)
        {
            UpdateScheduling(priority ?? Priority, weight ?? Weight);
        }
    }

    public void UpdateScheduling(int priority, int weight)
    {
        Priority = priority;
        Weight = weight;
    }

    public void UpdateRateLimitScope(RateLimitScope rateLimitScope)
    {
        RateLimitScope = rateLimitScope;

        if (rateLimitScope == RateLimitScope.Account)
        {
            LimitedModels = null;
            if (Status == AccountStatus.PartiallyRateLimited)
            {
                Status = AccountStatus.Normal;
                StatusDescription = null;
                RateLimitDurationSeconds = null;
                LastStatusUpdateTime = DateTime.UtcNow;
            }
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
        LimitedModels = null;
        Status = AccountStatus.RateLimited;
        RateLimitDurationSeconds = (int)lockDuration.TotalSeconds;
        LockedUntil = DateTime.UtcNow.AddSeconds((int)lockDuration.TotalSeconds);
        StatusDescription = description?.Length > 512 ? description[..512] : description;
        LastStatusUpdateTime = DateTime.UtcNow;

        AddLocalEvent(new AccountCircuitBrokenEvent(Id, lockDuration, StatusDescription));
    }

    public void MarkAsModelRateLimited(string modelKey, string? displayName, TimeSpan lockDuration, string? description)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var lockedUntil = now.AddSeconds((int)lockDuration.TotalSeconds);
        var activeModels = GetActiveLimitedModelsInternal(now);
        activeModels.RemoveAll(model => string.Equals(model.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase));
        activeModels.Add(new LimitedModelState
        {
            ModelKey = modelKey,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modelKey : displayName,
            LockedUntil = lockedUntil,
            StatusDescription = description?.Length > 512 ? description[..512] : description
        });

        LimitedModels = activeModels;
        Status = AccountStatus.PartiallyRateLimited;
        RateLimitDurationSeconds = null;
        LockedUntil = null;
        StatusDescription = $"部分模型限流中（{activeModels.Count}）";
        LastStatusUpdateTime = now;
    }

    public void MarkAsError(string? description)
    {
        LimitedModels = null;
        Status = AccountStatus.Error;
        RateLimitDurationSeconds = null;
        LockedUntil = null;
        StatusDescription = description?.Length > 512 ? description[..512] : description;
        LastStatusUpdateTime = DateTime.UtcNow;
    }

    public bool ClearModelRateLimit(string? modelKey)
    {
        var now = DateTime.UtcNow;
        var activeModels = GetActiveLimitedModelsInternal(now);
        var changed = false;

        if (!string.IsNullOrWhiteSpace(modelKey))
        {
            changed = activeModels.RemoveAll(model => string.Equals(model.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase)) > 0;
        }
        else if (LimitedModels is { Count: > 0 })
        {
            changed = activeModels.Count != LimitedModels.Count;
        }

        if (!changed)
        {
            return NormalizePartialStatus(activeModels, now);
        }

        LimitedModels = activeModels.Count > 0 ? activeModels : null;
        return NormalizePartialStatus(activeModels, now, true);
    }

    public bool ResetStatus()
    {
        var hadStatus = Status != AccountStatus.Normal || LockedUntil.HasValue || RateLimitDurationSeconds.HasValue || (LimitedModels?.Count > 0);
        if (!hadStatus)
            return false;

        LimitedModels = null;
        Status = AccountStatus.Normal;
        RateLimitDurationSeconds = null;
        LockedUntil = null;
        StatusDescription = null;
        LastStatusUpdateTime = DateTime.UtcNow;
        AddLocalEvent(new AccountRecoveredEvent(Id));
        return true;
    }

    /// <summary>
    /// 获取当前有效状态（限流过期时返回 Normal，模型级限流返回 PartiallyRateLimited）
    /// </summary>
    public AccountStatus GetEffectiveStatus()
    {
        if (Status == AccountStatus.Error)
        {
            return AccountStatus.Error;
        }

        if (Status == AccountStatus.RateLimited && !IsRateLimitExpired())
        {
            return AccountStatus.RateLimited;
        }

        return GetActiveLimitedModels().Count > 0 ? AccountStatus.PartiallyRateLimited : AccountStatus.Normal;
    }

    /// <summary>
    /// 判断账户级限流是否已过期
    /// </summary>
    public bool IsRateLimitExpired()
    {
        return Status == AccountStatus.RateLimited && LockedUntil.HasValue && LockedUntil.Value <= DateTime.UtcNow;
    }

    public IReadOnlyList<LimitedModelState> GetActiveLimitedModels()
    {
        return GetActiveLimitedModelsInternal(DateTime.UtcNow);
    }

    public bool IsModelRateLimited(string? modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return false;
        }

        return GetActiveLimitedModels().Any(model => string.Equals(model.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAvailable()
    {
        if (!IsActive) return false;

        return GetEffectiveStatus() != AccountStatus.Error && GetEffectiveStatus() != AccountStatus.RateLimited;
    }

    public bool IsModelAvailable(string? modelKey)
    {
        if (!IsAvailable()) return false;
        if (string.IsNullOrWhiteSpace(modelKey)) return true;
        return !IsModelRateLimited(modelKey);
    }

    private List<LimitedModelState> GetActiveLimitedModelsInternal(DateTime nowUtc)
    {
        return (LimitedModels ?? [])
            .Where(model => !model.IsExpired(nowUtc))
            .OrderBy(model => model.LockedUntil)
            .ToList();
    }

    private bool NormalizePartialStatus(List<LimitedModelState> activeModels, DateTime nowUtc, bool forceChanged = false)
    {
        if (activeModels.Count > 0)
        {
            LimitedModels = activeModels;
            if (Status != AccountStatus.RateLimited)
            {
                Status = AccountStatus.PartiallyRateLimited;
                StatusDescription = $"部分模型限流中（{activeModels.Count}）";
                RateLimitDurationSeconds = null;
                LockedUntil = null;
                LastStatusUpdateTime = nowUtc;
                return true;
            }

            return forceChanged;
        }

        if (Status == AccountStatus.PartiallyRateLimited || forceChanged)
        {
            LimitedModels = null;
            Status = AccountStatus.Normal;
            StatusDescription = null;
            RateLimitDurationSeconds = null;
            LockedUntil = null;
            LastStatusUpdateTime = nowUtc;
            AddLocalEvent(new AccountRecoveredEvent(Id));
            return true;
        }

        return false;
    }
}
