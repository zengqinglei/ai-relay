using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.ProviderGroups.Repositories;

public interface IProviderGroupAccountRelationRepository : IRepository<ProviderGroupAccountRelation, Guid>
{
    /// <summary>
    /// 获取分组下的有效关联列表（包含 AccountToken 信息）
    /// </summary>
    Task<List<ProviderGroupAccountRelation>> GetListByGroupIdWithAccountsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
