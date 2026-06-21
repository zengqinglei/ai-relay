using AiRelay.Domain.ApiKeys.Events;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ApiKeys.Entities;

/// <summary>
/// API Key 实体
/// </summary>
public class ApiKey : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 归属用户 Id
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// API 密钥加密值（AES 加密存储）
    /// </summary>
    public string EncryptedSecret { get; private set; }

    /// <summary>
    /// API 密钥哈希值（HMAC-SHA256，确定性，用于唯一性查找）
    /// </summary>
    public string SecretHash { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// 过期时间（null 表示永不过期）
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    public virtual ICollection<ApiKeyProviderGroupBinding> Bindings { get; private set; } = new List<ApiKeyProviderGroupBinding>();

    // ── 计费统计字段 ──────────────────────────────────────────────────────────

    /// <summary>今日调用次数（本地自然日，跨日自动归零）</summary>
    public long UsageToday { get; private set; }

    /// <summary>累计调用次数</summary>
    public long UsageTotal { get; private set; }

    /// <summary>今日消耗额度（USD）</summary>
    public decimal CostToday { get; private set; }

    /// <summary>累计消耗额度（USD）</summary>
    public decimal CostTotal { get; private set; }

    /// <summary>今日消耗 Token 数</summary>
    public long TokensToday { get; private set; }

    /// <summary>累计消耗 Token 数</summary>
    public long TokensTotal { get; private set; }

    /// <summary>今日成功次数</summary>
    public long SuccessToday { get; private set; }

    /// <summary>累计成功次数</summary>
    public long SuccessTotal { get; private set; }

    /// <summary>今日统计基准锚点（本地 00:00 对应的 UTC 时间）</summary>
    public DateTime? StatsDate { get; private set; }

    /// <summary>
    /// 累加统计数据，跨日自动重置今日字段
    /// </summary>
    /// <param name="tokens">消耗的 token</param>
    /// <param name="cost">消耗的费用</param>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="todayUtcAnchor">当前的本地零点 UTC 锚点（由领域服务注入）</param>
    public void AccumulateStats(long tokens, decimal cost, bool isSuccess, DateTime todayUtcAnchor)
    {
        UsageTotal++;
        TokensTotal += tokens;
        CostTotal += cost;
        if (isSuccess) SuccessTotal++;

        if (StatsDate != todayUtcAnchor)
        {
            UsageToday = 1;
            TokensToday = tokens;
            CostToday = cost;
            SuccessToday = isSuccess ? 1 : 0;
            StatsDate = todayUtcAnchor;
        }
        else
        {
            UsageToday++;
            TokensToday += tokens;
            CostToday += cost;
            if (isSuccess) SuccessToday++;
        }
    }

    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }

    public bool IsValid()
    {
        return IsActive && !IsExpired() && !IsDeleted;
    }

    private ApiKey()
    {
        Name = null!;
        EncryptedSecret = null!;
        SecretHash = null!;
    }

    public ApiKey(
        Guid userId,
        string name,
        string? description,
        string encryptedSecret,
        string secretHash,
        DateTime? expiresAt = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Name = name;
        Description = description;
        EncryptedSecret = encryptedSecret;
        SecretHash = secretHash;
        ExpiresAt = expiresAt;

        AddLocalEvent(new ApiKeyCreatedEvent(Id, Name));
    }

    public void Enable() => IsActive = true;

    public void Disable() => IsActive = false;

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void UpdateExpiration(DateTime? expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public void RecordUsage(DateTime nowUtc)
    {
        LastUsedAt = nowUtc;
    }

    public void MarkAsDeleted()
    {
        AddLocalEvent(new ApiKeyDeletedEvent(Id, Name, EncryptedSecret));
    }
}
