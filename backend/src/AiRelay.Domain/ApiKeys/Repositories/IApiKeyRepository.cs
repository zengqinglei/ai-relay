using AiRelay.Domain.ApiKeys.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.ApiKeys.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    /// <summary>
    /// 获取带绑定信息的实体（用于删除等仅依赖绑定导航的场景）
    /// </summary>
    Task<ApiKey?> GetWithBindingsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取详情（包含绑定分组及路由推导所需信息）
    /// </summary>
    Task<ApiKey?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分页列表（包含绑定信息）
    /// </summary>
    Task<(long TotalCount, List<ApiKey> Items)> GetPagedListAsync(
        string? keyword,
        bool? isActive,
        int offset,
        int limit,
        string? sorting = null,
        CancellationToken cancellationToken = default);
}
