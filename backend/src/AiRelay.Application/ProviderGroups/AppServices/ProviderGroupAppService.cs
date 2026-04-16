using System.Linq.Dynamic.Core;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ProviderGroups.AppServices;

/// <summary>
/// 提供商分组应用服务实现
/// </summary>
public class ProviderGroupAppService(
    IRepository<ProviderGroup, Guid> providerGroupRepository,
    IRepository<ProviderGroupAccountRelation, Guid> relationRepository,
    ProviderGroupDomainService providerGroupDomainService,
    ILogger<ProviderGroupAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter) : BaseAppService(), IProviderGroupAppService
{
    #region 分组管理

    public async Task<ProviderGroupOutputDto> CreateAsync(CreateProviderGroupInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建分组 {Name}...", input.Name);

        var group = await providerGroupDomainService.CreateGroupAsync(
            input.Name,
            input.Description,
            input.EnableStickySession,
            input.StickySessionExpirationHours,
            input.RateMultiplier,
            cancellationToken);

        logger.LogInformation("创建分组成功 (ID: {Id})", group.Id);
        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task<ProviderGroupOutputDto> UpdateAsync(Guid id, UpdateProviderGroupInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新分组 {Id}...", id);

        var existingGroup = await providerGroupRepository.GetByIdAsync(id, cancellationToken);
        if (existingGroup == null)
            throw new NotFoundException($"分组不存在: {id}");

        var group = await providerGroupDomainService.UpdateGroupAsync(
            id,
            input.Name,
            input.Description,
            input.EnableStickySession,
            input.StickySessionExpirationHours,
            input.RateMultiplier,
            cancellationToken);

        logger.LogInformation("更新分组成功 (ID: {Id})", id);
        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始删除分组 {Id}...", id);
        await providerGroupDomainService.DeleteGroupAsync(id, cancellationToken);
        logger.LogInformation("删除分组成功 (ID: {Id})", id);
    }

    public async Task<ProviderGroupOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetByIdAsync(id, cancellationToken);
        if (group == null)
            throw new NotFoundException($"分组不存在: {id}");

        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task<PagedResultDto<ProviderGroupOutputDto>> GetPagedListAsync(GetProviderGroupPagedInputDto input, CancellationToken cancellationToken = default)
    {
        var query = await providerGroupRepository.GetQueryableAsync(cancellationToken);

        // 名称模糊搜索
        if (!string.IsNullOrWhiteSpace(input.Keyword))
            query = query.Where(g => g.Name.Contains(input.Keyword));



        // 动态排序
        var sorting = input.Sorting ?? $"{nameof(ProviderGroup.CreationTime)} desc";
        query = query.OrderBy(sorting);

        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
        var providerGroups = await asyncExecuter.ToListAsync(query.Skip(input.Offset).Take(input.Limit), cancellationToken);
        var result = objectMapper.Map<List<ProviderGroup>, List<ProviderGroupOutputDto>>(providerGroups);

        // 批量加载关联的账户
        if (providerGroups.Count > 0)
        {
            var summaryByGroupId = await GetGroupSummaryByIdsAsync(providerGroups.Select(g => g.Id).ToList(), cancellationToken);

            foreach (var groupDto in result)
            {
                if (summaryByGroupId.TryGetValue(groupDto.Id, out var summary))
                {
                    groupDto.AccountCount = summary.AccountCount;
                    groupDto.SupportedRouteProfiles = summary.SupportedRouteProfiles;
                }
                else
                {
                    groupDto.AccountCount = 0;
                    groupDto.SupportedRouteProfiles = [];
                }
            }
        }

        return new PagedResultDto<ProviderGroupOutputDto>(totalCount, result);
    }

    #endregion

    #region Private Methods

    private async Task<ProviderGroupOutputDto> MapToOutputDtoAsync(ProviderGroup group, CancellationToken cancellationToken)
    {
        var result = objectMapper.Map<ProviderGroup, ProviderGroupOutputDto>(group);

        var summaryByGroupId = await GetGroupSummaryByIdsAsync([group.Id], cancellationToken);
        if (summaryByGroupId.TryGetValue(group.Id, out var summary))
        {
            result.AccountCount = summary.AccountCount;
            result.SupportedRouteProfiles = summary.SupportedRouteProfiles;
        }
        else
        {
            result.AccountCount = 0;
            result.SupportedRouteProfiles = [];
        }

        return result;
    }

    private async Task<Dictionary<Guid, (int AccountCount, List<RouteProfile> SupportedRouteProfiles)>> GetGroupSummaryByIdsAsync(
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        var relationQuery = await relationRepository.GetQueryableAsync(cancellationToken);
        var relations = await asyncExecuter.ToListAsync(
            relationQuery
                .Include(r => r.AccountToken)
                .Where(r => groupIds.Contains(r.ProviderGroupId) && r.AccountToken != null),
            cancellationToken);

        return relations
            .GroupBy(r => r.ProviderGroupId)
            .ToDictionary(
                group => group.Key,
                group => (
                    AccountCount: group.Count(),
                    SupportedRouteProfiles: ResolveRouteProfiles(group
                        .Where(r => r.AccountToken != null)
                        .Select(r => (r.AccountToken!.Provider, r.AccountToken!.AuthMethod))
                        .ToHashSet())));
    }

    /// <summary>
    /// 从账号的 (Provider, AuthMethod) 组合反查 RouteProfileRegistry，得出该分组能响应的路由协议列表
    /// </summary>
    private static List<RouteProfile> ResolveRouteProfiles(HashSet<(Provider Provider, AuthMethod AuthMethod)> combinations)
    {
        if (combinations.Count == 0)
        {
            return [];
        }

        return RouteProfileRegistry.Profiles
            .Where(p => p.Value.SupportedCombinations.Any(c => combinations.Contains((c.Provider, c.AuthMethod))))
            .Select(p => p.Key)
            .OrderBy(p => p)
            .ToList();
    }

    private sealed record ProviderGroupSummary(int AccountCount, List<RouteProfile> SupportedRouteProfiles);

    #endregion
}
