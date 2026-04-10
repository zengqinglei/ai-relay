using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy;

/// <summary>
/// 平台路由元数据
/// 用于 app.Map(...).WithMetadata() 标记路由对应的平台
/// </summary>
public record PlatformMetadata(RouteProfile Profile);
