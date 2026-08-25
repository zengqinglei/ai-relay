using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;

namespace AiRelay.Api.HostedServices.BackgroundServices;

/// <summary>
/// 上游模型缓存后台预热服务
/// 定期刷新所有活跃账号的上游模型缓存，确保选号热路径（IsModelSupportedAsync）永远命中缓存，
/// 避免请求时产生 N×10s 上游超时延迟。
/// 仅刷新未配置白名单和映射的账号（配置了白名单/映射的账号不依赖上游拉取）。
/// </summary>
public class UpstreamModelCacheRefreshService(
    IServiceProvider serviceProvider,
    ILogger<UpstreamModelCacheRefreshService> logger) : BackgroundService
{
    // 启动延迟：等待应用完全就绪、数据库连接池稳定
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    // 刷新间隔：须小于正常缓存 TTL 最小值（30min），确保缓存永不冷启动
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(20);

    // 账号间延迟：防止对上游造成请求风暴（10 账号 ≈ 10s/轮）
    private static readonly TimeSpan AccountDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "上游模型缓存预热服务已启动，刷新间隔: {Interval}min，账号间延迟: {Delay}s",
            RefreshInterval.TotalMinutes, AccountDelay.TotalSeconds);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRefreshAsync(stoppingToken);

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("上游模型缓存预热服务已停止");
    }

    private async Task RunRefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var accountRepo = scope.ServiceProvider
                .GetRequiredService<IRepository<AccountToken, Guid>>();
            var domainService = scope.ServiceProvider
                .GetRequiredService<AccountTokenDomainService>();

            var allAccounts = (await accountRepo.GetListAsync(
                a => a.IsActive && !a.IsDeleted && a.Status != AccountStatus.Error,
                stoppingToken)).ToList();

            var handlerFactory = scope.ServiceProvider.GetRequiredService<IChatModelHandlerFactory>();

            // 仅刷新需要上游拉取的账号：未配置白名单，且对应的 ChatModelHandler 覆写了 GetModelsAsync 的平台
            // 注意：不检查 ModelMapping，即使配置了映射也去拉取上游以刷新缓存（满足特定需求）
            var targets = allAccounts
                .Where(a =>
                {
                    if (a.ModelWhites != null && a.ModelWhites.Count > 0)
                        return false;

                    try
                    {
                        var handler = handlerFactory.CreateHandler(a.Provider, a.AuthMethod);
                        var method = handler.GetType().GetMethod(nameof(IChatModelHandler.GetModelsAsync));
                        // 判断方法是否有具体实现（非 BaseChatModelHandler 的默认实现）
                        return method != null && method.DeclaringType?.Name != nameof(BaseChatModelHandler);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            logger.LogInformation(
                "后台模型缓存刷新开始: 总活跃账号={Total}, 需刷新={Targets}",
                allAccounts.Count, targets.Count);

            var success = 0;
            var fail = 0;

            foreach (var account in targets)
            {
                if (stoppingToken.IsCancellationRequested) break;

                logger.LogDebug("开始处理账号: Name={Name}, Provider={Provider}, BaseUrl={Url}",
                    account.Name, account.Provider, account.BaseUrl);

                try
                {
                    await domainService.RefreshTokenIfNeededAsync(account, stoppingToken);
                    var models = await domainService.FetchAndCacheUpstreamModelsAsync(account, stoppingToken);
                    if (models != null)
                    {
                        success++;
                        logger.LogDebug("账号处理成功: Name={Name}, Count={Count}", account.Name, models.Count);
                    }
                    else
                    {
                        fail++;
                        logger.LogWarning("账号处理失败（返回null）: Name={Name}", account.Name);
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("账号处理被取消: Name={Name}", account.Name);
                    break;
                }
                catch (UnauthorizedException ex)
                {
                    // 不可恢复凭证错误：Domain 层已调用 MarkAsError 并持久化
                    // 此处仅记 Warning，下次轮询该账号将因 Status=Error 被过滤跳过
                    fail++;
                    logger.LogWarning(
                        "账号凭证失效已标记为 Error，后续轮询将自动跳过: Name={Name}, Provider={Provider}, Message={Message}",
                        account.Name, account.Provider, ex.Message);
                }
                catch (Exception ex)
                {
                    fail++;
                    logger.LogWarning(ex,
                        "后台刷新账号模型缓存失败: Name={Name}, Provider={Provider}, Error={Error}",
                        account.Name, account.Provider, ex.Message);
                }

                // 账号间节流，避免对上游造成请求风暴
                try { await Task.Delay(AccountDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            logger.LogInformation(
                "后台模型缓存刷新完成: 成功={Success}, 失败/跳过={Fail}",
                success, fail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "后台模型缓存刷新任务执行失败: {Message}", ex.Message);
        }
    }
}
