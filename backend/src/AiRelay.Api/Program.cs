using AiRelay.Api.Authentication;
using AiRelay.Api.Extensions;
using AiRelay.Api.HostedServices.Initializer;
using AiRelay.Api.HostedServices.Workers;
using AiRelay.Api.Middleware.SmartProxy;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

using AiRelay.Application;
using AiRelay.Domain;
using AiRelay.Domain.Auth.Options;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.Json;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Domain.Users.Options;
using AiRelay.Infrastructure;
using AiRelay.Infrastructure.Persistence;
using Leistd.DependencyInjection;
using Leistd.Exception.AspNetCore;
using Leistd.Security.AspNetCore;
using Leistd.Tracing.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web application ...");
    var builder = WebApplication.CreateBuilder(args);

    // 1. 基础架构设置 (DI Factory, Logging, WebServer, HttpClient)
    builder.Host.UseServiceProviderFactory(new ServiceRegistrationCallbackFactory());

    builder.AddAiRelayInfrastructure();
    builder.Services.AddAiRelayWebServer();

    // 2. 业务层服务注册 (Domain -> Infrastructure -> Application)
    builder.Services.AddDomainServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApplicationServices();

    // 2.5. 配置选项
    builder.Services.Configure<DefaultAdminOptions>(
        builder.Configuration.GetSection(DefaultAdminOptions.SectionName));
    builder.Services.Configure<UsageLoggingOptions>(
        builder.Configuration.GetSection(UsageLoggingOptions.SectionName));
    builder.Services.Configure<ExternalAuthOptions>(
        builder.Configuration.GetSection(ExternalAuthOptions.SectionName));

    // ModelPricing 本地备份路径默认值（未配置时使用 ContentRootPath 下的 Resources 目录）
    builder.Services.PostConfigure<ModelPricingOptions>(options =>
    {
        if (string.IsNullOrWhiteSpace(options.LocalPath))
            options.LocalPath = Path.Combine(
                builder.Environment.ContentRootPath, "Resources", "model_pricing.json");
    });

    // 3. 注册应用启动引导程序 (替代手动 InitializeApplicationAsync)
    builder.Services.AddHostedService<ApplicationBootstrapper>();

    // 3.5. 注册后台服务
    // builder.Services.AddHostedService<AccountQuotaRefreshHostedService>();
    builder.Services.AddSingleton<AccountUsageRecordHostedService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AccountUsageRecordHostedService>());

    // [New] Register SmartProxy Components
    builder.Services.AddScoped<ProxyErrorFormatterFactory>();
    builder.Services.AddScoped<IProxyErrorFormatter, GeminiProxyErrorFormatter>();
    builder.Services.AddScoped<IProxyErrorFormatter, OpenAIProxyErrorFormatter>();
    builder.Services.AddScoped<IProxyErrorFormatter, ClaudeProxyErrorFormatter>();

    // 4. API 层基础设施 (Exception, HealthChecks, Controllers)
    builder.Services.AddGlobalExceptionHandler(builder.Configuration);
    builder.Services.AddHealthChecks();
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 使用 Domain.Shared 层的 WebApi 配置
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonOptions.WebApi.PropertyNamingPolicy;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonOptions.WebApi.DefaultIgnoreCondition;

            // 复制转换器
            foreach (var converter in JsonOptions.WebApi.Converters)
            {
                options.JsonSerializerOptions.Converters.Add(converter);
            }
        });

    // 4.1. CORS 配置
    builder.Services.AddCors(options =>
    {
        var corsConfig = builder.Configuration.GetSection("Cors");
        var allowAnyLocalhost = corsConfig.GetValue<bool>("AllowAnyLocalhost");
        var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();

            if (allowAnyLocalhost)
            {
                // 允许所有 Localhost 端口访问 (仅用于开发环境配置)
                policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost");
            }
            else if (allowedOrigins.Length > 0)
            {
                // 生产环境限制特定域名
                policy.WithOrigins(allowedOrigins);
            }
            // 否则默认不允许任何来源 (安全默认值)
        });
    });

    // 4.5. Leistd Security 服务
    builder.Services.AddLeistdSecurity();

    // 4.6. DataProtection 配置（生产环境必需）
    var redisConnStr = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnStr))
    {
        // 解析 Redis 连接字符串（支持 Upstash rediss:// URL）
        var redisConfig = ParseRedisConnectionString(redisConnStr);

        // 使用 Redis 持久化密钥
        builder.Services.AddDataProtection()
            .SetApplicationName("AiRelay")
            .PersistKeysToStackExchangeRedis(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig),
                "DataProtection-Keys");
    }
    else
    {
        // 开发环境：使用文件系统（容器重启会丢失）
        var keysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
        Directory.CreateDirectory(keysPath);
        builder.Services.AddDataProtection()
            .SetApplicationName("AiRelay")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    // 5. 安全配置 (AuthN & AuthZ)
    var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
        ?? throw new InvalidOperationException("JWT configuration is not configured");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        AuthenticationSchemes.ApiKey,
        null);

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.AiProxyPolicy, policy =>
        {
            policy.AuthenticationSchemes.Add(AuthenticationSchemes.ApiKey);
            policy.RequireAuthenticatedUser();
        });
    });

    // 6. 反向代理配置
    builder.Services.AddScoped<SmartReverseProxyMiddleware>();

    // --- 构建应用 ---
    var app = builder.Build();

    // ✅ 在应用启动前执行数据库迁移（确保数据库就绪后再接收请求）
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AiRelayDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        if (db.Database.IsRelational())
        {
            logger.LogInformation("检测到关系型数据库，正在应用数据库迁移...");
            await db.Database.MigrateAsync();
            logger.LogInformation("数据库迁移完成");
        }
        else
        {
            logger.LogInformation("使用内存数据库，跳过迁移");
        }
    }

    // 7. 中间件管道配置
    app.UseGlobalExceptionHandler();
    app.UseCorrelationId();
    app.MapHealthChecks("/api/health").AllowAnonymous();

    app.UseCors();

    app.UseLeistdSecurity();
    app.UseAuthentication();
    app.UseAuthorization();

    // 添加静态文件支持，用于托管前端应用
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();

    app.MapControllers();

    // 7. 显式路由映射 (替代原来的 MapReverseProxy)
    var proxyHandler = (HttpContext context) =>
        context.RequestServices.GetRequiredService<SmartReverseProxyMiddleware>().InvokeAsync(context);

    app.Map("/gemini/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.GEMINI_OAUTH))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/gemini-api/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.GEMINI_APIKEY))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/claude/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.CLAUDE_OAUTH))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/claude-api/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.CLAUDE_APIKEY))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/openai/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.OPENAI_OAUTH))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/openai-api/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.OPENAI_APIKEY))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    app.Map("/antigravity/{**catch-all}", proxyHandler)
       .WithMetadata(new PlatformMetadata(ProviderPlatform.ANTIGRAVITY))
       .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);

    // 兜底路由，指向前端的 index.html (SPA 支持)
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// 解析 Redis 连接字符串，支持 Upstash rediss:// URL 格式
/// </summary>
static StackExchange.Redis.ConfigurationOptions ParseRedisConnectionString(string connectionString)
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