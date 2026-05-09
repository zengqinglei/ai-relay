using AiRelay.Api.Authentication;
using AiRelay.Api.Extensions;
using AiRelay.Api.HostedServices.BackgroundServices;
using AiRelay.Api.HostedServices.Initializer;
using AiRelay.Api.HostedServices.Workers;
using AiRelay.Api.Middleware.SmartProxy;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

using System.Security.Cryptography.X509Certificates;
using AiRelay.Application;
using AiRelay.Application.ApiKeys.Options;
using AiRelay.Application.UsageRecords.Queue;
using AiRelay.Domain;
using AiRelay.Domain.Auth.Options;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.Json;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Domain.Users.Options;
using AiRelay.Infrastructure;
using AiRelay.Infrastructure.Persistence;
using Leistd.DependencyInjection;
using Leistd.Exception.AspNetCore;
using Leistd.Security.AspNetCore;
using Leistd.Tracing.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using AiRelay.Domain.Shared.Email.Options;

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
    builder.Services.AddOptions<DefaultAdminOptions>()
        .Bind(builder.Configuration.GetSection(DefaultAdminOptions.SectionName));
    builder.Services.AddOptions<UsageLoggingOptions>()
        .Bind(builder.Configuration.GetSection(UsageLoggingOptions.SectionName));
    builder.Services.AddOptions<UsageCleanupOptions>()
        .Bind(builder.Configuration.GetSection(UsageCleanupOptions.SectionName));
    builder.Services.AddOptions<ExternalAuthOptions>()
        .Bind(builder.Configuration.GetSection(ExternalAuthOptions.SectionName));
    builder.Services.AddOptions<OAuthOptions>()
        .Bind(builder.Configuration.GetSection(OAuthOptions.SectionName));
    builder.Services.AddOptions<DefaultProviderModelsOptions>()
        .Bind(builder.Configuration.GetSection(DefaultProviderModelsOptions.SectionName));
    builder.Services.AddOptions<UserRegistrationOptions>()
        .Bind(builder.Configuration.GetSection(UserRegistrationOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddOptions<SmtpOptions>()
        .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

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
    builder.Services.AddSingleton<AccountUsageRecordWorker>();
    builder.Services.AddSingleton<IUsageRecordQueue>(sp => sp.GetRequiredService<AccountUsageRecordWorker>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AccountUsageRecordWorker>());
    builder.Services.AddHostedService<UsageRecordCleanupBackgroundService>();


    // [New] Register SmartProxy Components
    builder.Services.AddScoped<ProxyErrorFormatterFactory>();
    builder.Services.AddScoped<IProxyErrorFormatter, GeminiProxyErrorFormatter>();
    builder.Services.AddScoped<IProxyErrorFormatter, OpenAIProxyErrorFormatter>();
    builder.Services.AddScoped<IProxyErrorFormatter, ClaudeProxyErrorFormatter>();

    // 4. API 层基础设施 (Exception, HealthChecks, Controllers)
    builder.Services.AddGlobalExceptionHandler(builder.Configuration);
    builder.Services.AddHealthChecks();
    builder.Services.AddAiRelaySpaProxy();
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
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost;
    });

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
    builder.Services.AddAiRelayDataProtection(builder.Configuration, builder.Environment);

    // 5. 安全配置 (AuthN & AuthZ)
    builder.Services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<AiRelayDbContext>();
        })
        .AddServer(options =>
        {
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserInfoEndpointUris("/connect/userinfo")
                .SetEndSessionEndpointUris("/connect/logout");

            options.AllowAuthorizationCodeFlow()
                .RequireProofKeyForCodeExchange();
            options.AllowRefreshTokenFlow();

            options.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Roles,
                OpenIddictConstants.Scopes.OfflineAccess);

            var oauthOptions = builder.Configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions();
            if (oauthOptions.UseDevelopmentCertificates)
            {
                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(oauthOptions.SigningCertificatePath) ||
                    string.IsNullOrWhiteSpace(oauthOptions.EncryptionCertificatePath))
                {
                    throw new InvalidOperationException("OAuth signing and encryption certificates must be configured when development certificates are disabled.");
                }

                options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                    oauthOptions.SigningCertificatePath,
                    oauthOptions.SigningCertificatePassword));
                options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                    oauthOptions.EncryptionCertificatePath,
                    oauthOptions.EncryptionCertificatePassword));
            }

            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough()
                .EnableEndSessionEndpointPassthrough();
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    })
    .AddCookie("AiRelayCookie", options =>
    {
        options.LoginPath = "/auth/login";
        options.Cookie.Name = "AiRelay.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;

        // 从配置读取过期天数，默认7天
        var cookieExpireDays = builder.Configuration.GetValue<int>($"{OAuthOptions.SectionName}:CookieExpireDays", 7);
        options.ExpireTimeSpan = TimeSpan.FromDays(cookieExpireDays);
        options.SlidingExpiration = true;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        AuthenticationSchemes.ApiKey,
        null);

    builder.Services.AddAuthorization(options =>
    {
        // 覆盖默认策略，使其同时支持 Bearer Token 和 Cookie 认证
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, "AiRelayCookie")
            .RequireAuthenticatedUser()
            .Build();

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
    app.UseForwardedHeaders();
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/.well-known") ||
            context.Request.Path.StartsWithSegments("/connect"))
        {
            app.Logger.LogInformation(
                "OpenID request after forwarded headers: Path={Path}, Scheme={Scheme}, Host={Host}, RemoteIp={RemoteIp}, XForwardedProto={XForwardedProto}, XForwardedHost={XForwardedHost}, XForwardedFor={XForwardedFor}",
                context.Request.Path,
                context.Request.Scheme,
                context.Request.Host,
                context.Connection.RemoteIpAddress,
                context.Request.Headers["X-Forwarded-Proto"].ToString(),
                context.Request.Headers["X-Forwarded-Host"].ToString(),
                context.Request.Headers["X-Forwarded-For"].ToString());
        }

        await next();
    });
    // 官方推荐最佳实践 1：将静态文件托管移至 UseSerilogRequestLogging 之前，
    // 这样静态资源的请求直接在此处返回，完全不会进入后续的 Serilog 记录中间件。
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null || httpContext.Response.StatusCode >= 500)
                return Serilog.Events.LogEventLevel.Error;

            // 官方推荐最佳实践 2：通过 Endpoint 特征精确降级特定端点（如 Vite SPA Proxy）
            var endpoint = httpContext.GetEndpoint();
            if (endpoint != null && string.Equals(endpoint.DisplayName, "SpaProxyFallback", StringComparison.OrdinalIgnoreCase))
            {
                return Serilog.Events.LogEventLevel.Verbose;
            }

            return Serilog.Events.LogEventLevel.Information;
        };
    });
    app.UseGlobalExceptionHandler();
    app.UseCorrelationId();
    app.MapHealthChecks("/api/health").AllowAnonymous();

    app.UseCors();

    app.UseLeistdSecurity();
    app.UseAuthentication();
    app.UseAuthorization();



    app.MapControllers();

    // 7. 显式路由映射 (替代原来的 MapReverseProxy)
    var proxyHandler = (HttpContext context) =>
        context.RequestServices.GetRequiredService<SmartReverseProxyMiddleware>().InvokeAsync(context);

    // Unified Routing based on RouteProfile definitions
    foreach (var profile in RouteProfileRegistry.Profiles)
    {
        // 允许带和不带后续路径
        // 对于包含冒号分隔符的路径（如 /v1internal），使用冒号连接 catch-all
        // 对于普通路径，使用斜杠连接 catch-all
        string routePattern;
        if (profile.Value.PathPrefix.Contains(':'))
        {
            routePattern = $"{profile.Value.PathPrefix}:{{**catch-all}}";
        }
        else
        {
            routePattern = $"{profile.Value.PathPrefix}/{{**catch-all}}";
        }

        app.Map(routePattern, proxyHandler)
            .WithMetadata(new PlatformMetadata(profile.Key))
            .RequireAuthorization(AuthorizationPolicies.AiProxyPolicy);
    }

    app.MapAiRelaySpaFallback();

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

