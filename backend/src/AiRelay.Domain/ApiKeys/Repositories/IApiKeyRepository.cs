using AiRelay.Domain.ApiKeys.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.ApiKeys.Repositories;

public interface IApiKeyRepository : IRepository<ApiKey, Guid>
{
    /// <summary>
    /// 获取详情（包含绑定信息）
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
        CancellationToken cancellationToken = default);
}
