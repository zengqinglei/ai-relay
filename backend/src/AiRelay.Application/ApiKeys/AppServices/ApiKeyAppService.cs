using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Application.ApiKeys.Options;
using AiRelay.Domain.ApiKeys.DomainServices;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ApiKeys.Repositories;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Shared.Security.Aes;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Users.Specifications;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.Lock.Core;
using Leistd.ObjectMapping.Core;
using Leistd.Security.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.ApiKeys.AppServices;

public class ApiKeyAppService(
    ApiKeyDomainService apiKeyDomainService,
    IApiKeyRepository apiKeyRepository,
    IProviderGroupRepository providerGroupRepository,
    ProviderGroupDomainService providerGroupDomainService,
    IRepository<User, Guid> userRepository,
    ILogger<ApiKeyAppService> logger,
    IObjectMapper objectMapper,
    IAesEncryptionProvider aesEncryptionProvider,
    IOptions<DefaultProviderModelsOptions> defaultProviderModelsOptions,
    IDistributedLock distributedLock,
    ICurrentUser currentUser) : BaseAppService, IApiKeyAppService
{
    public async Task<ApiKeyOutputDto> CreateAsync(CreateApiKeyInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建 API Key {Name}...", input.Name);

        var bindings = input.Bindings?.Select(b => (b.Priority, b.ProviderGroupId)).ToList()
                       ?? [];
        await EnsureBindingsAccessibleAsync(bindings.Select(x => x.ProviderGroupId).ToList(), cancellationToken);

        var apiKey = await apiKeyDomainService.CreateWithKeyAsync(
            currentUser.Id!.Value,
            input.Name,
            input.Description,
            input.CustomSecret,
            input.ExpiresAt,
            bindings,
            cancellationToken);

        var contextItems = new Dictionary<string, object>();

        var result = objectMapper.Map<ApiKey, ApiKeyOutputDto>(apiKey, contextItems);
        result.Secret = DecryptSecret(apiKey.EncryptedSecret);
        await FillOwnerInfoAsync([result], cancellationToken);

        logger.LogInformation("创建 ApiKey 成功 (ID: {Id})", apiKey.Id);
        return result;
    }

    public async Task<ApiKeyOutputDto> UpdateAsync(Guid id, UpdateApiKeyInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新 ApiKey {Id}...", id);

        var apiKey = RequireAccessibleApiKey(await apiKeyRepository.GetWithDetailsAsync(id, cancellationToken), id);
        var bindings = input.Bindings?.Select(b => (b.Priority, b.ProviderGroupId)).ToList();
        await EnsureBindingsAccessibleAsync(bindings?.Select(x => x.ProviderGroupId).ToList() ?? [], cancellationToken);

        await apiKeyDomainService.UpdateAsync(
            apiKey,
            input.Name,
            input.Description,
            input.ExpiresAt,
            bindings!,
            cancellationToken);

        logger.LogInformation("更新 ApiKey 成功 (ID: {Id})", id);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始删除 ApiKey {Id}...", id);

        RequireAccessibleApiKey(await apiKeyRepository.GetByIdAsync(id, cancellationToken), id);
        await apiKeyDomainService.DeleteAsync(id, cancellationToken);
        logger.LogInformation("删除 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task EnableAsync(Guid id, DateTime? newExpiresAt = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始启用 ApiKey {Id}...", id);

        RequireAccessibleApiKey(await apiKeyRepository.GetByIdAsync(id, cancellationToken), id);
        await apiKeyDomainService.EnableAsync(id, newExpiresAt, cancellationToken);
        logger.LogInformation("启用 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始禁用 ApiKey {Id}...", id);

        RequireAccessibleApiKey(await apiKeyRepository.GetByIdAsync(id, cancellationToken), id);
        await apiKeyDomainService.DisableAsync(id, cancellationToken);
        logger.LogInformation("禁用 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task<ApiKeyOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = RequireAccessibleApiKey(await apiKeyRepository.GetWithDetailsAsync(id, cancellationToken), id);
        var contextItems = new Dictionary<string, object>();

        var result = objectMapper.Map<ApiKey, ApiKeyOutputDto>(apiKey, contextItems);
        result.Secret = DecryptSecret(apiKey.EncryptedSecret);
        await FillOwnerInfoAsync([result], cancellationToken);
        return result;
    }

    public async Task<PagedResultDto<ApiKeyOutputDto>> GetPagedListAsync(GetApiKeyPagedInputDto input, CancellationToken cancellationToken = default)
    {
        var (totalCount, apiKeys) = await apiKeyRepository.GetPagedListAsync(
            input.Keyword,
            input.IsActive,
            input.Offset,
            input.Limit,
            input.Sorting,
            UserScopeSpecifications.ResolveScopedUserId(currentUser, input.OnlyCurrentUser),
            cancellationToken);

        if (apiKeys.Count == 0)
        {
            return new PagedResultDto<ApiKeyOutputDto>(totalCount, []);
        }

        var results = objectMapper.Map<List<ApiKey>, List<ApiKeyOutputDto>>(apiKeys);
        for (var i = 0; i < apiKeys.Count; i++)
        {
            results[i].Secret = DecryptSecret(apiKeys[i].EncryptedSecret);
        }

        await FillOwnerInfoAsync(results, cancellationToken);
        return new PagedResultDto<ApiKeyOutputDto>(totalCount, results);
    }

    public async Task<DefaultProviderModelsOutputDto> GetDefaultProviderModelsAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var apiKey = await EnsureDefaultApiKeyAsync(cancellationToken);
        var options = defaultProviderModelsOptions.Value;

        return new DefaultProviderModelsOutputDto
        {
            ApiKey = new DefaultApiKeyOutputDto
            {
                Name = apiKey.Name,
                Secret = DecryptSecret(apiKey.EncryptedSecret)
            },
            Endpoints = BuildDefaultProviderModelEndpoints(options, $"{baseUrl.TrimEnd('/')}/v1")
        };
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string plainKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey))
        {
            return ApiKeyValidationResult.Failure("API Key 不能为空");
        }

        var apiKey = await apiKeyDomainService.ValidateKeyAsync(plainKey, cancellationToken);
        if (apiKey == null)
        {
            return ApiKeyValidationResult.Failure("API Key 无效、已禁用或已过期");
        }

        return ApiKeyValidationResult.Success(apiKey);
    }

    private async Task<ApiKey> EnsureDefaultApiKeyAsync(CancellationToken cancellationToken)
    {
        const string defaultApiKeyName = "default";
        var userId = currentUser.Id!.Value;
        var existingApiKey = await apiKeyRepository.GetFirstAsync(
            x => x.UserId == userId && x.Name == defaultApiKeyName,
            cancellationToken: cancellationToken);
        if (existingApiKey != null)
        {
            return existingApiKey;
        }

        await using var handle = await distributedLock.LockAsync($"api-key:default:{userId}", cancellationToken);

        existingApiKey = await apiKeyRepository.GetFirstAsync(
            x => x.UserId == userId && x.Name == defaultApiKeyName,
            cancellationToken: cancellationToken);
        if (existingApiKey != null)
        {
            return existingApiKey;
        }

        await providerGroupDomainService.EnsureDefaultProviderGroupAsync(cancellationToken);

        var defaultGroup = await providerGroupRepository.GetFirstAsync(x => x.IsDefault, cancellationToken: cancellationToken);
        if (defaultGroup == null)
        {
            throw new BadRequestException("默认供应商分组不存在");
        }

        return await apiKeyDomainService.CreateWithKeyAsync(
            userId,
            defaultApiKeyName,
            "系统自动创建的默认 API Key",
            null,
            null,
            [(1, defaultGroup.Id)],
            cancellationToken);
    }

    private static IReadOnlyList<DefaultProviderModelEndpointOutputDto> BuildDefaultProviderModelEndpoints(
        DefaultProviderModelsOptions options,
        string baseUrl)
    {
        return
        [
            new DefaultProviderModelEndpointOutputDto
            {
                Id = $"{options.ProviderIdPrefix}-completions",
                Protocol = "openai-completions",
                BaseUrl = baseUrl,
                Models = options.Models
            }
        ];
    }

    private string DecryptSecret(string encryptedSecret)
    {
        try
        {
            return string.IsNullOrEmpty(encryptedSecret) ? string.Empty : aesEncryptionProvider.Decrypt(encryptedSecret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "解密 API Key 失败");
            return "***DECRYPT_ERROR***";
        }
    }

    private ApiKey RequireAccessibleApiKey(ApiKey? apiKey, Guid id)
    {
        if (apiKey == null)
        {
            throw new UnauthorizedException($"ApiKey 不存在: {id}");
        }

        if (apiKey.UserId != currentUser.Id!.Value && !UserScopeSpecifications.IsAdmin(currentUser))
        {
            throw new UnauthorizedException($"ApiKey 不存在: {id}");
        }

        return apiKey;
    }

    private async Task FillOwnerInfoAsync(List<ApiKeyOutputDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var userIds = items.Select(x => x.UserId).Distinct().ToList();
        var users = await userRepository.GetListAsync(x => userIds.Contains(x.Id), cancellationToken);
        var userLookup = users.ToDictionary(x => x.Id);

        foreach (var item in items)
        {
            if (!userLookup.TryGetValue(item.UserId, out var user))
            {
                continue;
            }

            item.Username = user.Username;
            item.Email = user.Email;
        }
    }

    private async Task EnsureBindingsAccessibleAsync(IReadOnlyCollection<Guid> providerGroupIds, CancellationToken cancellationToken)
    {
        if (providerGroupIds.Count == 0)
        {
            return;
        }

        var visibleGroupIds = (await providerGroupRepository.GetVisibleGroupsAsync(currentUser.Id!.Value, cancellationToken))
            .Select(x => x.Id)
            .ToHashSet();
        var invalidIds = providerGroupIds.Where(x => !visibleGroupIds.Contains(x)).Distinct().ToList();
        if (invalidIds.Count > 0)
        {
            throw new UnauthorizedException($"分组不可访问: {string.Join(", ", invalidIds)}");
        }
    }
}
