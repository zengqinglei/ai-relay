using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ApiKeys.Repositories;
using AiRelay.Domain.ProviderGroups.Repositories;
using Leistd.Ddd.Infrastructure;
using Leistd.Ddd.Infrastructure.Auditing;
using Leistd.Ddd.Infrastructure.EventBus;
using Leistd.EventBus.Local;
using Leistd.Lock.Redis;
using Leistd.Lock.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AiRelay.Infrastructure.Persistence;

using AiRelay.Domain.Shared.Security.Aes;
using AiRelay.Domain.Shared.Security.Aes.Options;
using AiRelay.Domain.Shared.Security.Jwt;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using AiRelay.Domain.Shared.Security.PasswordHash;
using AiRelay.Infrastructure.Shared.Security.Aes;
using AiRelay.Infrastructure.Shared.Security.Jwt;
using AiRelay.Infrastructure.Shared.Security.PasswordHash;

using AiRelay.Infrastructure.Persistence.Repositories;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Google;
using AiRelay.Infrastructure.Shared.OAuth.Authorize;
using AiRelay.Infrastructure.Shared.OAuth.Google;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.BackgroundJobs;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Infrastructure.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

namespace AiRelay.Infrastructure;

/// <summary>
/// Infrastructure 层依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 Infrastructure 层服务
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册本地事件总线
        services.AddLocalEventBus();

        // ✅ 注册 SaveChanges 拦截器（必须在 AddDbContext 之前）
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<LocalEventSaveChangesInterceptor>();

        // ✅ 注册基础设施服务
        services.AddMemoryCache();      // IMemoryCache - 用于定价缓存等

        // ... (Existing options configuration) ...
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<ModelPricingOptions>(configuration.GetSection(ModelPricingOptions.SectionName));

        services.Configure<EncryptionOptions>(options =>
        {
            options.Key = configuration["ENCRYPTION_KEY"];
        });

        // ✅ 注册 DbContext（使用拦截器）
        services.AddDbContext<AiRelayDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("AiRelay");
            }

            // ✅ 添加 SaveChanges 拦截器（EF Core 官方推荐的最佳实践）
            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<LocalEventSaveChangesInterceptor>());
        });

        // 注册 DDD Infrastructure 基础服务（UnitOfWork + 自动仓储注册）
        // DbContext 注册后会自动注册对应的仓储
        services.AddDddInfrastructure();

        // 注册具体的 Repository
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IProviderGroupAccountRelationRepository, ProviderGroupAccountRelationRepository>();

        // ... (Rest of the method) ...
        // 分布式缓存 + 分布式锁（优先 Redis，否则内存降级）
        var redisConnStr = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnStr))
        {
            // 解析 Redis 连接字符串（支持 Upstash URL）
            var redisConfig = ParseRedisConnectionString(redisConnStr);

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisConfig;
                options.InstanceName = "AiRelay:";
            });

            services.AddRedisDistributedLock(redisConfig.ToString());

            services.AddTransient<IConcurrencyStrategy, RedisConcurrencyStrategy>();
        }
        else
        {
            services.AddDistributedMemoryCache();
            services.AddMemoryLocalLock();
            services.AddTransient<IConcurrencyStrategy, NoOpConcurrencyStrategy>();
        }


        // 模型提供者服务（Singleton，因为映射规则和模型目录是全局静态的）
        services.AddTransient<IModelProvider, ModelProvider>();

        // ✅ 签名缓存服务（Singleton 确保全局唯一，用于 Thinking Block 签名链传递）
        services.AddSingleton<ISignatureCache, InMemorySignatureCache>();

        // 聊天模型客户端工厂
        services.AddTransient<IChatModelHandlerFactory, ChatModelHandlerFactory>();

        // 注册 Chat Model Clients (使用 Strategy 模式，通过 Supports 方法自选)
        services.AddTransient<IChatModelHandler, GeminiAccountChatModelHandler>();
        services.AddTransient<IChatModelHandler, GeminiApiChatModelHandler>();

        services.AddTransient<IChatModelHandler, ClaudeChatModelHandler>();
        // ClaudeChatModelHandler 同时支持 Account 和 ApiKey，只需注册一次

        services.AddTransient<IChatModelHandler, OpenAiChatModelHandler>();
        // OpenAiChatModelHandler 同时支持 Account 和 ApiKey，只需注册一次

        // 新增 Antigravity 客户端
        services.AddTransient<IChatModelHandler, AntigravityChatModelHandler>();

        // (Removed HttpRequestSender)

        // SSE 响应流处理器
        services.AddTransient<SseResponseStreamProcessor>();

        // 密码哈希服务（无状态，使用 Transient 生命周期）
        services.AddTransient<IPasswordHasher, PasswordHasher>();

        // JWT Token 服务（无状态，使用 Transient 生命周期）
        services.AddTransient<IJwtTokenProvider, JwtTokenProvider>();

        // AES 加密服务（无状态，使用 Transient 生命周期）
        services.AddTransient<IAesEncryptionProvider, AesEncryptionProvider>();

        // 注册 OAuth 提供商服务 (Keyed 模式)
        // 1. Google 系 (同时支持 "google" 字符串用于系统登录)
        services.AddKeyedTransient<IOAuthProvider, GoogleOAuthProvider>("google");
        services.AddKeyedTransient<IOAuthProvider, GoogleOAuthProvider>(ProviderPlatform.GEMINI_OAUTH);
        services.AddKeyedTransient<IOAuthProvider, GoogleOAuthProvider>(ProviderPlatform.ANTIGRAVITY);

        // 2. Claude 系
        services.AddKeyedTransient<IOAuthProvider, ClaudeOAuthProvider>(ProviderPlatform.CLAUDE_OAUTH);

        // 3. OpenAI 系
        services.AddKeyedTransient<IOAuthProvider, OpenAiOAuthProvider>(ProviderPlatform.OPENAI_OAUTH);

        // 4. GitHub (用于系统登录 "github" 字符串 Key)
        services.AddKeyedTransient<IOAuthProvider, GitHubOAuthProvider>("github");

        // Google OAuth 配置服务
        services.AddTransient<IGoogleAuthConfigService, GoogleAuthConfigService>();

        // OAuth 会话管理器
        services.AddTransient<IOAuthSessionManager, OAuthSessionManager>();

        // ✅ 签名缓存清理后台服务（定期清理过期签名）
        services.AddHostedService<SignatureCacheCleanupBackgroundService>();
        // [New] Pricing Update Background Service (定价更新后台服务)
        services.AddHostedService<PricingUpdateBackgroundService>();

        return services;

    }

    /// <summary>
    /// 解析 Redis 连接字符串，支持 Upstash rediss:// URL 格式
    /// </summary>
    private static StackExchange.Redis.ConfigurationOptions ParseRedisConnectionString(string connectionString)
    {
        // 如果是 redis:// 或 rediss:// URL 格式，手动解析
        if (connectionString.StartsWith("redis://") || connectionString.StartsWith("rediss://"))
        {
            var uri = new Uri(connectionString);
            var config = new StackExchange.Redis.ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port } },
                Ssl = uri.Scheme == "rediss",
                AbortOnConnectFail = false,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                KeepAlive = 60
            };

            // 解析用户名和密码
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':');
                if (parts.Length == 2)
                {
                    config.User = parts[0];
                    config.Password = parts[1];
                }
                else if (parts.Length == 1)
                {
                    config.Password = parts[0];
                }
            }

            return config;
        }

        // 否则使用默认解析
        return StackExchange.Redis.ConfigurationOptions.Parse(connectionString);
    }
}
