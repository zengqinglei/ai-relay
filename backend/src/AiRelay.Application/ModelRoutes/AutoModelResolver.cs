using AiRelay.Application.ApiKeys.Options;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using Leistd.Exception.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.ModelRoutes;

/// <summary>
/// auto 模型解析器 — 代理中间件与工作区聊天两条链路共用。
/// 负责：候选模型列表构建、session 粘性缓存的读写、ModelFailoverContext 初始化。
/// 粘性缓存优先使用上次成功的模型，否则从候选列表第一个开始，后续由 failover 机制兜底。
/// </summary>
public class AutoModelResolver(
    IOptions<DefaultProviderModelsOptions> defaultProviderModelsOptions,
    IDistributedCache cache)
{
    public const string AutoModelId = "auto";

    private const string StickyKeyPrefix = "sticky:auto-model:";
    private static readonly TimeSpan StickyTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// 为 auto 请求解析初始模型（写入 downContext.ResolvedModelId）并构建 failover 上下文。
    /// 非 auto 请求返回 null，不影响现有逻辑。
    /// </summary>
    public async Task<ModelFailoverContext?> ResolveAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(downContext.ModelId, AutoModelId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var models = defaultProviderModelsOptions.Value.Models
            .Where(m => !string.Equals(m, AutoModelId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (models.Length == 0)
        {
            throw new BadRequestException("DefaultProviderModels 未配置任何有效的候选模型（排除 auto 后为空）");
        }

        var startIndex = 0;
        if (!string.IsNullOrEmpty(downContext.SessionId))
        {
            // 分布式缓存的 Get 会自动刷新滑动过期，无需显式 Refresh
            var cachedModel = await cache.GetStringAsync(StickyKey(downContext.SessionId), cancellationToken);
            if (!string.IsNullOrEmpty(cachedModel))
            {
                var idx = Array.FindIndex(models, m => string.Equals(m, cachedModel, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) startIndex = idx;
            }
        }

        downContext.ResolvedModelId = models[startIndex];
        return new ModelFailoverContext
        {
            CandidateModels = models,
            CurrentModelIndex = startIndex
        };
    }

    /// <summary>
    /// 路由成功后持久化本次实际使用的模型（粘性缓存，滑动过期 1 小时）。
    /// 仅对 auto 请求且路由成功时调用。
    /// </summary>
    public async Task SaveStickyModelAsync(
        DownRequestContext downContext,
        ModelFailoverContext? failoverContext,
        CancellationToken cancellationToken)
    {
        if (failoverContext == null ||
            downContext.ResolvedModelId == null ||
            string.IsNullOrEmpty(downContext.SessionId))
        {
            return;
        }

        await cache.SetStringAsync(
            StickyKey(downContext.SessionId),
            downContext.ResolvedModelId,
            new DistributedCacheEntryOptions { SlidingExpiration = StickyTtl },
            cancellationToken);
    }

    private static string StickyKey(string sessionId) => $"{StickyKeyPrefix}{sessionId}";
}
