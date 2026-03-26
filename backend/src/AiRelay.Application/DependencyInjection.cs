using AiRelay.Application.ApiKeys.AppServices;
using AiRelay.Application.ApiKeys.EventHandlers;
using AiRelay.Application.Auth.AppServices;
using AiRelay.Application.Initialization;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderAccounts.EventHandlers;
using AiRelay.Application.ProviderGroups.AppServices;
using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.Users.AppServices;
using AiRelay.Domain.ApiKeys.Events;
using Leistd.EventBus.Core.EventHandler;
using Leistd.ObjectMapping.AutoMapper;
using Leistd.Ddd.Application.Permission;
using Microsoft.Extensions.DependencyInjection;
using AiRelay.Application.Permissions.Checker;
using AiRelay.Application.Permissions.Provider;
using AiRelay.Domain.ProviderAccounts.Events;

using AiRelay.Application.ApiKeys.Mappings;
using AiRelay.Application.ProviderAccounts.Mappings;
using AiRelay.Application.ProviderGroups.Mappings;
using AiRelay.Application.Users.Mappings;

namespace AiRelay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapperObjectMapper(options =>
        {
            options.Configurators.Add(cfg => cfg.AddMaps(typeof(DependencyInjection).Assembly));
        });

        // Register AutoMapper Resolvers
        services.AddTransient<AccountTokenConcurrencyResolver>();
        services.AddTransient<ApiKeySecretResolver>();
        services.AddTransient<ApiKeyStatsMappingAction>();
        services.AddTransient<GroupRelationConcurrencyResolver>();
        services.AddTransient<UserRolesResolver>();

        services.AddTransient<ISystemInitializer, SystemInitializer>();
        services.AddTransient<IAccountTokenAppService, AccountTokenAppService>();
        services.AddTransient<IApiKeyAppService, ApiKeyAppService>();
        services.AddTransient<IApiKeyMetricAppService, ApiKeyMetricAppService>();

        // Smart Proxy AppService
        services.AddScoped<ISmartProxyAppService, SmartProxyAppService>();

        // Provider Groups
        services.AddScoped<IProviderGroupAppService, ProviderGroupAppService>();

        // Identity & Authorization Services
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IExternalAuthAppService, ExternalAuthAppService>();

        // Permission
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddSingleton<IPermissionDefinitionProvider, PermissionDefinitionProvider>();
        services.AddSingleton<IPermissionDefinitionManager, PermissionDefinitionManager>();

        services.AddScoped<IEventHandler<ApiKeyCreatedEvent>, ApiKeyCreatedEventHandler>();
        services.AddScoped<IEventHandler<ApiKeyDeletedEvent>, ApiKeyDeletedEventHandler>();

        // 领域事件处理器
        services.AddScoped<IEventHandler<AccountDisabledEvent>, AccountDisabledEventHandler>();
        services.AddScoped<IEventHandler<AccountCircuitBrokenEvent>, AccountCircuitBrokenEventHandler>();
        services.AddScoped<IEventHandler<AccountRecoveredEvent>, AccountRecoveredEventHandler>();

        // 提供商账户应用服务
        services.AddScoped<AccountFingerprintAppService>();
        services.AddScoped<IAccountQuotaAppService, AccountQuotaAppService>();
        services.AddScoped<IAccountTokenMetricAppService, AccountTokenMetricAppService>();

        // Usage & Traffic
        services.AddTransient<IUsageLifecycleAppService, UsageLifecycleAppService>();
        services.AddTransient<IUsageRecordMetricAppService, UsageRecordMetricAppService>();
        services.AddTransient<IUsageRecordAppService, UsageRecordAppService>();

        return services;
    }
}