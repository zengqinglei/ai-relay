using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.ProviderGroups.Repositories;

/// <summary>
/// 提供商分组仓储接口
/// </summary>
public interface IProviderGroupRepository : IRepository<ProviderGroup, Guid>
{
    /// <summary>
    /// 获取包含详情的分组
    /// </summary>
    Task<ProviderGroup?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}
