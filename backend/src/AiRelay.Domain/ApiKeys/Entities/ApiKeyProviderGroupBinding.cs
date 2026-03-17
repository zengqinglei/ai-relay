using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ApiKeys.Entities;

/// <summary>
/// ApiKey 与提供商分组的绑定关系
/// </summary>
public class ApiKeyProviderGroupBinding : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// ApiKey ID
    /// </summary>
    public Guid ApiKeyId { get; private set; }

    /// <summary>
    /// 平台类型
    /// </summary>
    public ProviderPlatform Platform { get; private set; }

    /// <summary>
    /// 分组ID
    /// </summary>
    public Guid ProviderGroupId { get; private set; }

    // 导航属性
    public ApiKey? ApiKey { get; private set; }
    public ProviderGroup ProviderGroup { get; private set; } = null!;

    // EF Core 私有构造函数
    private ApiKeyProviderGroupBinding() { }

    /// <summary>
    /// 创建绑定
    /// </summary>
    public ApiKeyProviderGroupBinding(
        Guid apiKeyId,
        ProviderPlatform platform,
        Guid providerGroupId)
    {
        Id = Guid.CreateVersion7();
        ApiKeyId = apiKeyId;
        Platform = platform;
        ProviderGroupId = providerGroupId;
    }

    /// <summary>
    /// 更新绑定的分组
    /// </summary>
    public void UpdateGroup(Guid providerGroupId) => ProviderGroupId = providerGroupId;
}
