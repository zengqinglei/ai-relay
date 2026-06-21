using AiRelay.Application.ModelRoutes.Dtos;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.ModelRoutes;

public interface IModelRouteAppService : IAppService
{
    /// <summary>
    /// 解析代理入口候选范围，返回可供统一调度执行的候选组集合
    /// </summary>
    Task<IReadOnlyList<RouteAccountSchedulingGroup>> ResolveProxyRouteCandidatesAsync(
        SelectProxyAccountInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析工作区入口候选范围，返回可供统一调度执行的候选组集合
    /// </summary>
    Task<IReadOnlyList<RouteAccountSchedulingGroup>> ResolveWorkspaceRouteCandidatesAsync(
        SelectWorkspaceAccountInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 统一的路由执行大循环（包含重试、并发控制、切号、埋点写入和流健康检查）
    /// </summary>
    Task ExecuteRouteAsync(
        DownRequestContext baseDownContext,
        RouteExecutionMetadata metadata,
        IReadOnlyList<RouteAccountSchedulingGroup> candidateGroups,
        Func<SelectAccountResultDto, DownRequestContext> downContextModifier,
        IRouteResponseHandler responseHandler,
        CancellationToken cancellationToken);

    /// <summary>
    /// 聚合当前 ApiKey 绑定分组内可用的模型列表（供 /v1/models 端点调用）。
    /// 仅读缓存，不发起上游网络请求，按协议格式返回。
    /// </summary>
    Task<ProxyModelsOutputDto> GetProxyModelsAsync(
        Guid apiKeyId,
        string responseFormat,
        CancellationToken cancellationToken = default);
}
