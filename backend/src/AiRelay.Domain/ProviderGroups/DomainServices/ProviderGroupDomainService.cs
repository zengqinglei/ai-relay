using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;

namespace AiRelay.Domain.ProviderGroups.DomainServices;

/// <summary>
/// 提供商分组领域服务
/// </summary>
public class ProviderGroupDomainService(
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository providerGroupAccountRelationRepository,
    IRepository<User, Guid> userRepository)
{
    private const string DefaultGroupName = "default";

    /// <summary>
    /// 确保系统内置 default 分组存在
    /// </summary>
    public async Task EnsureDefaultProviderGroupAsync(CancellationToken cancellationToken = default)
    {
        var defaultGroup = await providerGroupRepository.GetFirstAsync(
            g => g.IsDefault || g.Name == DefaultGroupName,
            q => q.OrderByDescending(g => g.IsDefault).ThenBy(g => g.Id),
            cancellationToken);

        if (defaultGroup != null)
        {
            if (!defaultGroup.IsDefault || !string.Equals(defaultGroup.Name, DefaultGroupName, StringComparison.OrdinalIgnoreCase))
            {
                defaultGroup.MarkAsDefault(DefaultGroupName);
                await providerGroupRepository.UpdateAsync(defaultGroup, cancellationToken);
            }

            return;
        }

        var group = new ProviderGroup(
            name: DefaultGroupName,
            description: "系统默认分组",
            isDefault: true,
            enableStickySession: true,
            stickySessionExpirationHours: 1,
            rateMultiplier: 1.0m);

        await providerGroupRepository.InsertAsync(group, cancellationToken);
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    public async Task<ProviderGroup> CreateGroupAsync(
        string name,
        string? description,
        IReadOnlyCollection<Guid>? assignedUserIds,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        CancellationToken cancellationToken = default)
    {
        if (await providerGroupRepository.CountAsync(g => g.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"已存在同名分组: {name}");
        }

        var normalizedAssignedUserIds = NormalizeAssignedUserIds(assignedUserIds);
        await EnsureAssignedUsersExistAsync(normalizedAssignedUserIds, cancellationToken);

        var group = new ProviderGroup(
            name: name,
            description: description,
            assignedUserIds: normalizedAssignedUserIds,
            enableStickySession: enableStickySession,
            stickySessionExpirationHours: stickySessionExpirationHours,
            rateMultiplier: rateMultiplier);

        await providerGroupRepository.InsertAsync(group, cancellationToken);
        return group;
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    public async Task<ProviderGroup> UpdateGroupAsync(
        Guid id,
        string name,
        string? description,
        IReadOnlyCollection<Guid>? assignedUserIds,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken)
            ?? throw new BadRequestException($"分组不存在: {id}");

        if (group.IsDefault && !string.Equals(name, DefaultGroupName, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("默认分组名称不可修改");
        }

        var normalizedAssignedUserIds = NormalizeAssignedUserIds(assignedUserIds);

        if (group.IsDefault && normalizedAssignedUserIds.Count > 0)
        {
            throw new BadRequestException("默认分组必须保持公开，不能分配给指定用户");
        }

        if (!string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase) &&
            await providerGroupRepository.CountAsync(g => g.Id != id && g.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"已存在同名分组: {name}");
        }

        await EnsureAssignedUsersExistAsync(normalizedAssignedUserIds, cancellationToken);

        group.Update(group.IsDefault ? DefaultGroupName : name, description, normalizedAssignedUserIds, rateMultiplier);
        group.UpdateStickySession(enableStickySession, stickySessionExpirationHours);
        await providerGroupRepository.UpdateAsync(group, cancellationToken);
        return group;
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public async Task DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken)
            ?? throw new BadRequestException($"分组不存在: {id}");

        if (group.IsDefault)
        {
            throw new BadRequestException("默认分组不可删除");
        }

        if (group.ApiKeyBindings.Any())
        {
            throw new BadRequestException("分组已被 ApiKey 绑定，无法删除");
        }

        await providerGroupRepository.DeleteAsync(group, cancellationToken);
    }

    /// <summary>
    /// 同步账户与分组关系（全量替换）
    /// </summary>
    public async Task SyncAccountRelationsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid>? providerGroupIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroupIds = await NormalizeProviderGroupIdsAsync(providerGroupIds, cancellationToken);
        var existingRelations = await providerGroupAccountRelationRepository.GetListAsync(r => r.AccountTokenId == accountId, cancellationToken);

        var existingGroupIdSet = existingRelations.Select(r => r.ProviderGroupId).ToHashSet();
        var targetGroupIdSet = normalizedGroupIds.ToHashSet();

        var relationsToDelete = existingRelations
            .Where(r => !targetGroupIdSet.Contains(r.ProviderGroupId))
            .ToList();

        if (relationsToDelete.Count > 0)
        {
            await providerGroupAccountRelationRepository.DeleteManyAsync(relationsToDelete, cancellationToken);
        }

        var groupIdsToAdd = normalizedGroupIds
            .Where(groupId => !existingGroupIdSet.Contains(groupId))
            .ToList();

        foreach (var groupId in groupIdsToAdd)
        {
            await providerGroupAccountRelationRepository.InsertAsync(
                new ProviderGroupAccountRelation(groupId, accountId),
                cancellationToken);
        }
    }

    /// <summary>
    /// 获取账户归属分组ID（空输入时自动回退到 default）
    /// </summary>
    public async Task<List<Guid>> NormalizeProviderGroupIdsAsync(
        IReadOnlyCollection<Guid>? providerGroupIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = providerGroupIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];

        if (normalizedIds.Count == 0)
        {
            return [await GetDefaultProviderGroupIdAsync(cancellationToken)];
        }

        var groups = await providerGroupRepository.GetListAsync(g => normalizedIds.Contains(g.Id), cancellationToken);
        if (groups.Count() != normalizedIds.Count)
        {
            var existingIdSet = groups.Select(g => g.Id).ToHashSet();
            var missingIds = normalizedIds.Where(id => !existingIdSet.Contains(id)).ToList();
            throw new NotFoundException($"分组不存在: {string.Join(", ", missingIds)}");
        }

        return normalizedIds;
    }

    private async Task<Guid> GetDefaultProviderGroupIdAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultProviderGroupAsync(cancellationToken);

        var defaultGroup = await providerGroupRepository.GetFirstAsync(
            g => g.IsDefault, 
            q => q.OrderBy(g => g.Id), 
            cancellationToken)
            ?? throw new NotFoundException("默认分组不存在");

        return defaultGroup.Id;
    }

    private static List<Guid> NormalizeAssignedUserIds(IReadOnlyCollection<Guid>? assignedUserIds)
    {
        return assignedUserIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList() ?? [];
    }

    private async Task EnsureAssignedUsersExistAsync(IReadOnlyCollection<Guid> assignedUserIds, CancellationToken cancellationToken)
    {
        if (assignedUserIds.Count == 0)
        {
            return;
        }

        var existingUsers = (await userRepository.GetListAsync(x => assignedUserIds.Contains(x.Id), cancellationToken)).ToList();
        if (existingUsers.Count != assignedUserIds.Count)
        {
            var existingUserIds = existingUsers.Select(x => x.Id).ToHashSet();
            var missingUserIds = assignedUserIds.Where(x => !existingUserIds.Contains(x)).ToList();
            throw new NotFoundException($"用户不存在: {string.Join(", ", missingUserIds)}");
        }
    }
}


