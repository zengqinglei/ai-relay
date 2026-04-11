using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.ProviderGroups.Repositories;

public interface IProviderGroupAccountRelationRepository : IRepository<ProviderGroupAccountRelation, Guid>
{
    /// <summary>
    /// 获取分组下的候选关系列表（预过滤协议、账号活跃度、排除列表，并包含 AccountToken 信息）
    /// </summary>
    Task<List<ProviderGroupAccountRelation>> GetCandidatesAsync(
        Guid groupId,
        List<(Provider Provider, AuthMethod AuthMethod)>? allowedCombinations = null,
        List<Guid>? excludedAccountIds = null,
        CancellationToken cancellationToken = default);
}
