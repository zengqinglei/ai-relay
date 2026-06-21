using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账号模型解析领域服务 — 统一 "账号对外暴露哪些模型" 的判定逻辑。
/// 用途：工作区聊天选模型、管理后台查账号模型、/v1/models 代理端点。
///   - ResolveExposedModelIdsAsync: 仅读缓存，不发起网络请求（绝对零 I/O，所有热路径使用）
/// </summary>
public class AccountModelResolverDomainService(
    AccountTokenDomainService accountTokenDomainService,
    IModelProvider modelProvider,
    ILogger<AccountModelResolverDomainService> logger)
{
    /// <summary>
    /// 解析账号对外暴露的模型 ID 列表（仅读缓存，不发起上游网络请求）。
    /// 优先级：白名单 → 映射 keys → 上游缓存 → Provider 静态目录
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveExposedModelIdsAsync(
        AccountToken account, CancellationToken ct = default)
    {
        var exposedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasConfig = false;

        // 1. 白名单优先（配置即权威）
        if (account.ModelWhites is { Count: > 0 })
        {
            var expanded = ExpandWhitelist(account.ModelWhites, account.Provider);
            foreach (var m in expanded) exposedModels.Add(m);
            hasConfig = true;
        }

        // 2. 映射 keys（客户端请求的是映射的 key，需对外暴露）
        if (account.ModelMapping is { Count: > 0 })
        {
            // 策略B：隐藏法，不对外暴露带有通配符的隐式/模糊模型（如 claude-haiku-*）
            var concreteMappingKeys = account.ModelMapping.Keys.Where(k => !k.Contains('*'));
            foreach (var m in concreteMappingKeys) exposedModels.Add(m);
            hasConfig = true;
        }

        if (hasConfig)
        {
            return [.. exposedModels];
        }

        // 3. 上游缓存（仅读，不穿透上游）
        var cached = await accountTokenDomainService.GetCachedModelIdsAsync(account.Id, ct);
        if (cached is { Count: > 0 })
            return [.. cached];

        // 4. Provider 静态目录兜底
        return modelProvider.GetAvailableModels(account.Provider)
            .Where(m => !m.Value.Contains('*'))
            .Select(m => m.Value)
            .ToList();
    }



    /// <summary>
    /// 根据账号的模型 ID 列表，从全局 Catalog 中解析出 ModelOption（含 Label/Category/Vendor 等展示元数据）。
    /// 找不到 Catalog 条目的直接用 ID 作 Label 兜底。
    /// </summary>
    public IReadOnlyList<ModelOption> ToModelOptions(
        IReadOnlyList<string> modelIds,
        IReadOnlyDictionary<string, ModelOption> catalogLookup)
    {
        return modelIds
            .Select(id => catalogLookup.TryGetValue(id, out var catalog) ? catalog : new ModelOption(id, id))
            .DistinctBy(m => m.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> ExpandWhitelist(IReadOnlyList<string> whitelist, Provider provider)
    {
        var catalog = modelProvider.GetAvailableModels(provider)
            .Where(m => !m.Value.Contains('*'))
            .ToList();

        return whitelist
            .SelectMany(pattern => pattern.Contains('*')
                ? catalog
                    .Where(m => AccountTokenDomainService.IsWildcardMatchPublic(m.Value, pattern))
                    .Select(m => m.Value)
                : (IEnumerable<string>)[pattern])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
