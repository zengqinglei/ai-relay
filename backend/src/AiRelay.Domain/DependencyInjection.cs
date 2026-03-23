using AiRelay.Domain.ApiKeys.DomainServices;
using AiRelay.Domain.Auth.DomainServices;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;
using AiRelay.Domain.UsageRecords.DomainServices;
using AiRelay.Domain.UsageRecords.Providers;
using AiRelay.Domain.Users.DomainServices;
using Microsoft.Extensions.DependencyInjection;

namespace AiRelay.Domain;

/// <summary>
/// Domain 层依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 Domain 层服务
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // 领域服务（无状态，使用 Transient 生命周期）
        services.AddTransient<AccountTokenDomainService>();
        services.AddTransient<AccountUsageCacheDomainService>();
        services.AddTransient<AccountRateLimitDomainService>();
        services.AddTransient<AccountResultHandlerDomainService>();
        services.AddTransient<AccountFingerprintDomainService>();
        services.AddTransient<ApiKeyDomainService>();

        // 统计领域服务
        services.AddTransient<AccountUsageStatisticsDomainService>();
        services.AddTransient<ApiKeyUsageStatisticsDomainService>();
        services.AddTransient<UsageRecordDomainService>();

        // 用户管理领域服务
        services.AddTransient<UserDomainService>();
        services.AddTransient<AuthDomainService>();
        services.AddTransient<UserRegistrationDomainService>();

        // 外部认证领域服务
        services.AddTransient<ExternalAuthDomainService>();

        // 提供商分组领域服务
        services.AddTransient<GroupSchedulingStrategyFactory>();
        services.AddTransient<ProviderGroupDomainService>();

        // [New] Pricing Provider (模型定价服务 - Singleton 因为有全局缓存和静态锁)
        services.AddSingleton<IPricingProvider, LiteLlmPricingProvider>();

        // 注册所有调度策略服务（统一使用 DI 管理）
        services.AddTransient<AdaptiveBalancedStrategy>();
        services.AddTransient<WeightedRandomStrategy>();
        services.AddTransient<PriorityStrategy>();
        services.AddTransient<QuotaPriorityStrategy>();

        return services;

    }

}

