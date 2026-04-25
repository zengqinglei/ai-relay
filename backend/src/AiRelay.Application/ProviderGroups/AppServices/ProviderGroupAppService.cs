using System.Linq.Dynamic.Core;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Users.Specifications;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Leistd.Security.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ProviderGroups.AppServices;

/// <summary>
/// 提供商分组应用服务实现
/// </summary>
public class ProviderGroupAppService(
    IProviderGroupRepository providerGroupRepository,
    IRepository<ProviderGroupAccountRelation, Guid> relationRepository,
    IRepository<User, Guid> userRepository,
    ProviderGroupDomainService providerGroupDomainService,
    ILogger<ProviderGroupAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter,
    ICurrentUser currentUser) : BaseAppService(), IProviderGroupAppService
{
    public async Task<ProviderGroupOutputDto> CreateAsync(CreateProviderGroupInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建分组 {Name}...", input.Name);

        var group = await providerGroupDomainService.CreateGroupAsync(
            input.Name,
            input.Description,
            input.AssignedUserIds,
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

        var existingGroup = await GetAccessibleGroupAsync(id, cancellationToken);
        if (existingGroup == null)
        {
            throw new NotFoundException($"分组不存在: {id}");
        }

        var group = await providerGroupDomainService.UpdateGroupAsync(
            id,
            input.Name,
            input.Description,
            input.AssignedUserIds,
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

        var existingGroup = await GetAccessibleGroupAsync(id, cancellationToken);
        if (existingGroup == null)
        {
            throw new NotFoundException($"分组不存在: {id}");
        }

        await providerGroupDomainService.DeleteGroupAsync(id, cancellationToken);
        logger.LogInformation("删除分组成功 (ID: {Id})", id);
    }

    public async Task<ProviderGroupOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await GetAccessibleGroupAsync(id, cancellationToken);
        if (group == null)
        {
            throw new NotFoundException($"分组不存在: {id}");
        }

        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task<PagedResultDto<ProviderGroupOutputDto>> GetPagedListAsync(GetProviderGroupPagedInputDto input, CancellationToken cancellationToken = default)
    {
        var query = await providerGroupRepository.GetQueryableAsync(cancellationToken);
        var currentUserId = currentUser.Id!.Value;
        query = query.Include(x => x.AssignedUsers);

        if (!UserScopeSpecifications.IsAdmin(currentUser) || input.OnlyCurrentUserVisible == true)
        {
            query = query.Where(x => !x.AssignedUsers.Any() || x.AssignedUsers.Any(y => y.UserId == currentUserId));
        }

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            query = query.Where(g => g.Name.Contains(input.Keyword) || (g.Description != null && g.Description.Contains(input.Keyword)));
        }

        if (input.AssignedUserId.HasValue)
        {
            query = query.Where(x => x.AssignedUsers.Any(y => y.UserId == input.AssignedUserId.Value));
        }

        if (input.IsPublic.HasValue)
        {
            query = input.IsPublic.Value
                ? query.Where(x => !x.AssignedUsers.Any())
                : query.Where(x => x.AssignedUsers.Any());
        }

        var sorting = input.Sorting ?? $"{nameof(ProviderGroup.CreationTime)} desc";
        query = query.OrderBy(sorting);

        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
        var providerGroups = await asyncExecuter.ToListAsync(query.Skip(input.Offset).Take(input.Limit), cancellationToken);
        var result = objectMapper.Map<List<ProviderGroup>, List<ProviderGroupOutputDto>>(providerGroups);

        await FillOutputAsync(providerGroups, result, cancellationToken);
        return new PagedResultDto<ProviderGroupOutputDto>(totalCount, result);
    }

    public async Task<List<ProviderGroupOutputDto>> GetVisibleListAsync(CancellationToken cancellationToken = default)
    {
        var providerGroups = await providerGroupRepository.GetVisibleGroupsAsync(currentUser.Id!.Value, cancellationToken);
        var result = objectMapper.Map<List<ProviderGroup>, List<ProviderGroupOutputDto>>(providerGroups);
        await FillOutputAsync(providerGroups, result, cancellationToken);
        return result;
    }

    private async Task<ProviderGroupOutputDto> MapToOutputDtoAsync(ProviderGroup group, CancellationToken cancellationToken)
    {
        var result = objectMapper.Map<ProviderGroup, ProviderGroupOutputDto>(group);
        await FillOutputAsync([group], [result], cancellationToken);
        return result;
    }

    private async Task FillOutputAsync(
        IReadOnlyCollection<ProviderGroup> groups,
        IReadOnlyCollection<ProviderGroupOutputDto> outputs,
        CancellationToken cancellationToken)
    {
        if (groups.Count == 0 || outputs.Count == 0)
        {
            return;
        }

        var summaryByGroupId = await GetGroupSummaryByIdsAsync(groups.Select(g => g.Id).ToList(), cancellationToken);
        var assignedUserIds = groups
            .SelectMany(x => x.AssignedUsers)
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        var users = assignedUserIds.Count == 0
            ? []
            : await userRepository.GetListAsync(x => assignedUserIds.Contains(x.Id), cancellationToken);
        var usernameLookup = users.ToDictionary(x => x.Id, x => x.Username);

        foreach (var output in outputs)
        {
            if (summaryByGroupId.TryGetValue(output.Id, out var summary))
            {
                output.AccountCount = summary.AccountCount;
                output.SupportedRouteProfiles = summary.SupportedRouteProfiles;
            }
            else
            {
                output.AccountCount = 0;
                output.SupportedRouteProfiles = [];
            }

            output.AssignedUsernames = groups
                .First(x => x.Id == output.Id)
                .AssignedUsers
                .Select(x => usernameLookup.GetValueOrDefault(x.UserId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
        }
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

    private async Task<ProviderGroup?> GetAccessibleGroupAsync(Guid id, CancellationToken cancellationToken)
    {
        if (UserScopeSpecifications.IsAdmin(currentUser))
        {
            return await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken);
        }

        return await providerGroupRepository.GetVisibleByIdAsync(id, currentUser.Id!.Value, cancellationToken);
    }
}
