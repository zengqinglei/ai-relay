using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Extensions;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;

namespace AiRelay.Application.ProviderGroups.AppServices;

public class SmartProxyAppService(
    ProviderGroupDomainService providerGroupDomainService,
    AccountTokenDomainService accountTokenDomainService,
    AccountResultHandlerDomainService accountResultHandlerDomainService,
    AccountUsageCacheDomainService usageCacheDomainService,
    IRepository<ApiKeyProviderGroupBinding, Guid> apiKeyProviderGroupBindingRepository,
    IRepository<AccountToken, Guid> accountRepository,
    IObjectMapper objectMapper,
    IConcurrencyStrategy concurrencyStrategy,
    IQueryableAsyncExecuter queryableAsyncExecuter,
    ILogger<SmartProxyAppService> logger) : BaseAppService, ISmartProxyAppService
{
    public async Task<SelectAccountResultDto> SelectAccountAsync(
        SelectProxyAccountInputDto input,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取 ApiKey 绑定的分组 ID
        var bindingGroupQuery = await apiKeyProviderGroupBindingRepository
            .GetQueryIncludingAsync(p => p.ProviderGroup);
        var bindingGroup = await queryableAsyncExecuter.FirstOrDefaultAsync(
            bindingGroupQuery.Where(b => b.ApiKeyId == input.ApiKeyId && b.Platform == input.Platform))
            ?? throw new ForbiddenException($"ApiKey '{input.ApiKeyName}' 未绑定 {input.Platform} 平台的分组，无法选择账户");

        // 2. 从分组中选择最佳账户（同时返回分组信息和粘性绑定状态）
        var result = await providerGroupDomainService.SelectAccountForApiKeyAsync(
            bindingGroup.ProviderGroupId,
            input.ApiKeyId,
            input.ApiKeyName,
            input.Platform,
            input.SessionHash,
            input.ExcludedAccountIds);

        if (result == null || result.Value.AccountToken == null)
        {
            throw new NotFoundException($"分组 {bindingGroup.ProviderGroup.Name} 中没有可用的 {input.Platform} 账户");
        }

        var (accountToken, providerGroup, isStickyBound, availableCount) = result.Value;

        // 3. 刷新 Token (如果需要)
        await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);

        if (string.IsNullOrEmpty(accountToken.AccessToken) && !input.Platform.IsApiKeyPlatform()) // 仅非APIKey平台检查AccessToken
        {
            throw new BadRequestException($"{accountToken.Platform} 账户 '{accountToken.Name}' 的凭证为空");
        }

        logger.LogDebug("已选定账户: {AccountTokenName} (ProviderGroupId: {ProviderGroupId}, IsStickyBound: {IsStickyBound})",
            accountToken.Name, providerGroup.Id, isStickyBound);

        // 决定等待策略
        var waitPlan = new WaitPlan
        {
            ShouldWait = isStickyBound,
            Timeout = isStickyBound ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(10), // 非粘性也给10s兜底等待
            MaxConcurrency = accountToken.MaxConcurrency,
            IsStickyBound = isStickyBound
        };

        // 预取并发数据
        var concurrencyCount = await concurrencyStrategy.GetConcurrencyCountAsync(accountToken.Id, cancellationToken);
        var contextItems = new Dictionary<string, object>
        {
            ["ConcurrencyCounts"] = new Dictionary<Guid, int> { [accountToken.Id] = concurrencyCount }
        };

        var accountTokenResult = objectMapper.Map<AccountToken, AvailableAccountTokenOutputDto>(accountToken, contextItems);

        return new SelectAccountResultDto
        {
            AccountToken = accountTokenResult,
            ProviderGroupId = providerGroup.Id,
            ProviderGroupName = providerGroup.Name,
            GroupRateMultiplier = providerGroup.RateMultiplier,
            WaitPlan = waitPlan,
            AvailableAccountCount = availableCount,
            AllowOfficialClientMimic = providerGroup.AllowOfficialClientMimic
        };
    }

    public async Task HandleSuccessAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await usageCacheDomainService.ClearBackoffCountAsync(accountId, cancellationToken);

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account == null) return;

        if (account.ResetStatus())
        {
            await accountRepository.UpdateAsync(account, cancellationToken);
        }
    }

    public async Task HandleFailureAsync(
        HandleFailureInputDto input,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(input.AccountId, cancellationToken);
        if (account == null) return;

        // 判断是否可重试错误
        var isRetryable = input.ErrorAnalysis.ErrorType == ModelErrorType.RateLimit ||
                          input.ErrorAnalysis.ErrorType == ModelErrorType.SignatureError ||
                          input.ErrorAnalysis.ErrorType == ModelErrorType.ServerError;

        // 调用领域服务执行熔断/禁用
        await accountResultHandlerDomainService.HandleFailureAsync(
            account,
            input.StatusCode,
            input.ErrorContent,
            isRetryable,
            input.RetryAfter,
            cancellationToken);

        await accountRepository.UpdateAsync(account, cancellationToken);
    }
}
