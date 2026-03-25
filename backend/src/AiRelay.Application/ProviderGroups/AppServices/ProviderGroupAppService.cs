using System.Linq.Dynamic.Core;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.ValueObjects;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;

namespace AiRelay.Application.ProviderGroups.AppServices;

/// <summary>
/// 提供商分组应用服务实现
/// </summary>
public class ProviderGroupAppService(
    IRepository<ProviderGroup, Guid> providerGroupRepository,
    IRepository<ProviderGroupAccountRelation, Guid> relationRepository,
    ProviderGroupDomainService providerGroupDomainService,
    IConcurrencyStrategy concurrencyStrategy,
    ILogger<ProviderGroupAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter) : BaseAppService(), IProviderGroupAppService
{
    #region 分组管理

    public async Task<ProviderGroupOutputDto> CreateAsync(CreateProviderGroupInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建分组 {Name}... 平台：{Platform}", input.Name, input.Platform);

        // 验证策略兼容性
        ValidateStrategyCompatibility(input.SchedulingStrategy, input.Platform);

        var accounts = input.Accounts?.Select(a => (a.AccountTokenId, a.Priority, a.Weight)).ToList()
                       ?? new List<(Guid AccountId, int Priority, int Weight)>();

        var group = await providerGroupDomainService.CreateGroupWithAccountsAsync(
            input.Name,
            input.Description,
            input.Platform,
            input.SchedulingStrategy,
            input.EnableStickySession,
            input.StickySessionExpirationHours,
            input.RateMultiplier,
            input.AllowOfficialClientMimic,
            accounts,
            cancellationToken);

        logger.LogInformation("创建分组成功 (ID: {Id})", group.Id);
        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task<ProviderGroupOutputDto> UpdateAsync(Guid id, UpdateProviderGroupInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新分组 {Id}...", id);

        // 获取当前分组以验证平台
        var existingGroup = await providerGroupRepository.GetByIdAsync(id, cancellationToken);
        if (existingGroup == null)
            throw new NotFoundException($"分组不存在: {id}");

        // 验证策略兼容性
        ValidateStrategyCompatibility(input.SchedulingStrategy, existingGroup.Platform);

        var accounts = input.Accounts?.Select(a => (a.AccountTokenId, a.Priority, a.Weight)).ToList()
                       ?? new List<(Guid AccountId, int Priority, int Weight)>();

        var group = await providerGroupDomainService.UpdateGroupWithAccountsAsync(
            id,
            input.Name,
            input.Description,
            input.SchedulingStrategy,
            input.EnableStickySession,
            input.StickySessionExpirationHours,
            input.RateMultiplier,
            input.AllowOfficialClientMimic,
            accounts,
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
            throw new BadRequestException($"分组不存在: {id}");

        return await MapToOutputDtoAsync(group, cancellationToken);
    }

    public async Task<PagedResultDto<ProviderGroupOutputDto>> GetPagedListAsync(GetProviderGroupPagedInputDto input, CancellationToken cancellationToken = default)
    {
        var query = await providerGroupRepository.GetQueryableAsync(cancellationToken);

        // 名称模糊搜索
        if (!string.IsNullOrWhiteSpace(input.Keyword))
            query = query.Where(g => g.Name.Contains(input.Keyword));

        // 平台筛选
        if (input.Platform.HasValue)
            query = query.Where(g => g.Platform == input.Platform.Value);

        // 动态排序
        var sorting = input.Sorting ?? $"{nameof(ProviderGroup.CreationTime)} desc";
        query = query.OrderBy(sorting);

        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
        var providerGroups = await asyncExecuter.ToListAsync(query
            .Skip(input.Offset)
            .Take(input.Limit), cancellationToken);

        var result = objectMapper.Map<List<ProviderGroup>, List<ProviderGroupOutputDto>>(providerGroups);

        // 批量加载关联的账户
        if (providerGroups.Any())
        {
            var groupIds = providerGroups.Select(g => g.Id).ToList();
            var relationQuery = await relationRepository.GetQueryIncludingAsync(r => r.AccountToken);
            var allRelations = await asyncExecuter.ToListAsync(relationQuery
                .Where(r => groupIds.Contains(r.ProviderGroupId)),
                cancellationToken);

            var relationsByGroup = allRelations
                .GroupBy(r => r.ProviderGroupId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 批量获取所有涉及账户的实时并发数
            var allAccountIds = allRelations.Select(r => r.AccountTokenId).Distinct().ToList();
            var concurrencyCounts = await concurrencyStrategy.GetConcurrencyCountsAsync(allAccountIds, cancellationToken);
            var contextItems = new Dictionary<string, object>
            {
                ["ConcurrencyCounts"] = concurrencyCounts
            };

            foreach (var groupDto in result)
            {
                if (relationsByGroup.TryGetValue(groupDto.Id, out var relations))
                {
                    groupDto.Accounts = objectMapper.Map<List<ProviderGroupAccountRelation>, List<GroupAccountRelationOutputDto>>(relations, contextItems);
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

        // 加载关联的账户
        var relationQuery = await relationRepository.GetQueryIncludingAsync(r => r.AccountToken);
        var relations = await asyncExecuter.ToListAsync(relationQuery
            .Where(r => r.ProviderGroupId == group.Id),
            cancellationToken);

        if (relations.Any())
        {
            var accountIds = relations.Select(a => a.AccountTokenId).Distinct().ToList();
            var concurrencyCounts = await concurrencyStrategy.GetConcurrencyCountsAsync(accountIds, cancellationToken);
            var contextItems = new Dictionary<string, object>
            {
                ["ConcurrencyCounts"] = concurrencyCounts
            };

            result.Accounts = objectMapper.Map<List<ProviderGroupAccountRelation>, List<GroupAccountRelationOutputDto>>(relations, contextItems);
        }
        else
        {
            result.Accounts = [];
        }

        return result;
    }

    /// <summary>
    /// 验证调度策略与平台的兼容性
    /// </summary>
    private static void ValidateStrategyCompatibility(GroupSchedulingStrategy strategy, ProviderPlatform platform)
    {
        if (strategy == GroupSchedulingStrategy.QuotaPriority)
        {
            var supportedPlatforms = new[] { ProviderPlatform.ANTIGRAVITY, ProviderPlatform.GEMINI_OAUTH };
            if (!supportedPlatforms.Contains(platform))
            {
                throw new BadRequestException(
                    $"QuotaPriority 策略仅支持 ANTIGRAVITY 和 GEMINI_OAUTH 平台，当前平台: {platform}");
            }
        }
    }

    #endregion
}
