using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.DomainServices;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ApiKeys.Repositories;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;

using AiRelay.Domain.Shared.Security.Aes;

namespace AiRelay.Application.ApiKeys.AppServices;

public class ApiKeyAppService(
    ApiKeyDomainService apiKeyDomainService,
    IApiKeyRepository apiKeyRepository,
    ILogger<ApiKeyAppService> logger,
    IObjectMapper objectMapper,
    IAesEncryptionProvider aesEncryptionProvider) : BaseAppService, IApiKeyAppService
{
    public async Task<ApiKeyOutputDto> CreateAsync(CreateApiKeyInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建 API Key {Name}...", input.Name);

        var bindings = input.Bindings?.Select(b => (b.Platform, b.ProviderGroupId)).ToList()
                       ?? new List<(Domain.ProviderAccounts.ValueObjects.ProviderPlatform, Guid)>();

        // 如果 CustomSecret 为空则自动生成，否则使用自定义值
        var apiKey = await apiKeyDomainService.CreateWithKeyAsync(
            input.Name,
            input.Description,
            input.CustomSecret,
            input.ExpiresAt,
            bindings,
            cancellationToken);

        // ✅ 统一传递上下文（即使当前统计为空）
        var contextItems = new Dictionary<string, object>();

        var result = objectMapper.Map<ApiKey, ApiKeyOutputDto>(apiKey, contextItems);
        result.Secret = DecryptSecret(apiKey.EncryptedSecret);

        logger.LogInformation("创建 ApiKey 成功 (ID: {Id})", apiKey.Id);
        return result;
    }

    public async Task<ApiKeyOutputDto> UpdateAsync(Guid id, UpdateApiKeyInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新 ApiKey {Id}...", id);

        var apiKey = await apiKeyRepository.GetWithDetailsAsync(id, cancellationToken);

        if (apiKey == null)
        {
            throw new UnauthorizedException($"ApiKey 不存在: {id}");
        }

        var bindings = input.Bindings?.Select(b => (b.Platform, b.ProviderGroupId)).ToList();

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
        await apiKeyDomainService.DeleteAsync(id, cancellationToken);
        logger.LogInformation("删除 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task EnableAsync(Guid id, DateTime? newExpiresAt = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始启用 ApiKey {Id}...", id);
        await apiKeyDomainService.EnableAsync(id, newExpiresAt, cancellationToken);
        logger.LogInformation("启用 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始禁用 ApiKey {Id}...", id);
        await apiKeyDomainService.DisableAsync(id, cancellationToken);
        logger.LogInformation("禁用 ApiKey 成功 (ID: {Id})", id);
    }

    public async Task<ApiKeyOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = await apiKeyRepository.GetWithDetailsAsync(id, cancellationToken);

        if (apiKey == null)
        {
            throw new UnauthorizedException($"API Key 不存在: {id}");
        }

        // 预取统计数据 (避免 Resolver 中的 Sync-over-Async)
        var contextItems = new Dictionary<string, object>();

        // 映射 (传递统计数据)
        var result = objectMapper.Map<ApiKey, ApiKeyOutputDto>(apiKey, contextItems);
        result.Secret = DecryptSecret(apiKey.EncryptedSecret);
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
            cancellationToken);

        if (apiKeys.Count == 0)
        {
            return new PagedResultDto<ApiKeyOutputDto>(totalCount, []);
        }

        // 映射
        var results = objectMapper.Map<List<ApiKey>, List<ApiKeyOutputDto>>(apiKeys);
        for (int i = 0; i < apiKeys.Count; i++)
        {
            results[i].Secret = DecryptSecret(apiKeys[i].EncryptedSecret);
        }

        return new PagedResultDto<ApiKeyOutputDto>(totalCount, results);
    }

    /// <summary>
    /// 验证 API Key
    /// </summary>
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
}