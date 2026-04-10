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
    /// 优先级 (数值越小优先级越高，如 1 为首选资源池)
    /// </summary>
    public int Priority { get; private set; }

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
        int priority,
        Guid providerGroupId)
    {
        Id = Guid.CreateVersion7();
        ApiKeyId = apiKeyId;
        Priority = priority;
        ProviderGroupId = providerGroupId;
    }

    /// <summary>
    /// 更新绑定的分组
    /// </summary>
    public void UpdateGroup(Guid providerGroupId) => ProviderGroupId = providerGroupId;
}
