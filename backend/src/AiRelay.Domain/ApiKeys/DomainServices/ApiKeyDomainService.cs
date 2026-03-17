using System.Security.Cryptography;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.Security.Aes;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ApiKeys.DomainServices;

/// <summary>
/// ApiKey 领域服务
/// </summary>
public class ApiKeyDomainService(
    IRepository<ApiKey, Guid> apiKeyRepository,
    IRepository<ApiKeyProviderGroupBinding, Guid> bindingRepository,
    IAesEncryptionProvider aesEncryptionProvider,
    ILogger<ApiKeyDomainService> logger)
{
    /// <summary>
    /// 创建 API Key（如果 customSecret 为空则自动生成）
    /// </summary>
    public async Task<ApiKey> CreateWithKeyAsync(
        string name,
        string? description,
        string? customSecret,
        DateTime? expiresAt,
        List<(ProviderPlatform Platform, Guid GroupId)> bindings,
        CancellationToken cancellationToken = default)
    {
        // 1. 唯一性校验（名称）
        if (await apiKeyRepository.CountAsync(k => k.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"API Key 名称 '{name}' 已存在");
        }

        // 2. 确定 Secret：使用自定义或自动生成
        var plainSecret = string.IsNullOrWhiteSpace(customSecret)
            ? GenerateApiKeySecret()
            : customSecret;

        // 3. 计算哈希（盲索引）
        var secretHash = aesEncryptionProvider.ComputeHash(plainSecret);

        // 4. 校验密钥唯一性 (核心优化：数据库索引查询 O(1))
        if (await apiKeyRepository.CountAsync(k => k.SecretHash == secretHash, cancellationToken) > 0)
        {
            throw new BadRequestException("此密钥已存在");
        }

        // 5. 加密密钥（用于展示）
        var encryptedSecret = aesEncryptionProvider.Encrypt(plainSecret);

        // 6. 创建实体
        var apiKey = new ApiKey(name, description, encryptedSecret, secretHash, expiresAt);

        // 7. 处理绑定
        await AddBindingsAsync(apiKey, bindings);

        await apiKeyRepository.InsertAsync(apiKey, cancellationToken: cancellationToken);

        logger.LogInformation("创建 API Key 成功: {Name} (ID: {Id})", name, apiKey.Id);
        return apiKey;
    }

    /// <summary>
    /// 更新 API Key
    /// </summary>
    public async Task UpdateAsync(
        ApiKey apiKey,
        string? name,
        string? description,
        DateTime? expiresAt,
        List<(ProviderPlatform Platform, Guid GroupId)> bindings,
        CancellationToken cancellationToken = default)
    {
        // 如果名称变更，检查唯一性
        if (!string.IsNullOrWhiteSpace(name) && name != apiKey.Name)
        {
            if (await apiKeyRepository.CountAsync(k => k.Name == name, cancellationToken) > 0)
            {
                throw new BadRequestException($"API Key 名称 '{name}' 已存在");
            }
        }

        apiKey.Update(name ?? apiKey.Name, description);
        apiKey.UpdateExpiration(expiresAt);

        // 绑定全量更新
        if (bindings != null)
        {
            await UpdateBindingsAsync(apiKey, bindings, cancellationToken);
        }

        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 删除 API Key
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (apiKey == null)
        {
            throw new BadRequestException($"API Key 不存在: {id}");
        }

        // 级联软删除绑定关系
        var bindings = await bindingRepository.GetListAsync(b => b.ApiKeyId == id, cancellationToken);
        if (bindings.Any())
        {
            await bindingRepository.DeleteManyAsync(bindings, cancellationToken);
        }

        await apiKeyRepository.DeleteAsync(apiKey, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 启用 API Key
    /// </summary>
    public async Task EnableAsync(Guid id, DateTime? newExpiresAt = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (apiKey == null) throw new BadRequestException($"API Key 不存在: {id}");

        if (newExpiresAt.HasValue)
        {
            apiKey.UpdateExpiration(newExpiresAt.Value);
        }

        if (apiKey.IsExpired())
        {
            throw new BadRequestException("API Key 已过期，无法启用。请先更新过期时间。");
        }

        apiKey.Enable();
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);

        logger.LogInformation("启用 API Key: {Name} (ID: {Id}), 过期时间: {ExpiresAt}",
            apiKey.Name, id, apiKey.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "永不过期");
    }

    /// <summary>
    /// 禁用 API Key
    /// </summary>
    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (apiKey == null) throw new BadRequestException($"API Key 不存在: {id}");

        apiKey.Disable();
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);

        logger.LogInformation("禁用 API Key: {Name} (ID: {Id})", apiKey.Name, id);
    }

    /// <summary>
    /// 验证 API Key
    /// </summary>
    /// <param name="plainSecret">明文密钥</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证成功返回 ApiKey 实体，失败返回 null</returns>
    public async Task<ApiKey?> ValidateKeyAsync(string plainSecret, CancellationToken cancellationToken = default)
    {
        // 1. 计算哈希
        var secretHash = aesEncryptionProvider.ComputeHash(plainSecret);

        // 2. 精确查找 (O(1))
        var apiKey = await apiKeyRepository.GetFirstAsync(k => k.SecretHash == secretHash, cancellationToken);

        if (apiKey == null)
        {
            logger.LogWarning("验证 API Key 失败: 密钥不存在");
            return null;
        }

        // 3. 验证是否有效（未过期、未删除、已启用）
        if (!apiKey.IsValid())
        {
            logger.LogWarning("验证 API Key 失败: {Name} (ID: {Id}) - 已禁用、已过期或已删除",
                apiKey.Name, apiKey.Id);
            return null;
        }

        // 4. 记录使用
        apiKey.RecordUsage();
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);

        logger.LogInformation("验证 API Key 成功: {Name} (ID: {Id})", apiKey.Name, apiKey.Id);
        return apiKey;
    }

    /// <summary>
    /// 生成新的 API Key（格式：sk-xxx，总长度约23位）
    /// 使用加密安全的随机数生成器
    /// </summary>
    private string GenerateApiKeySecret()
    {
        const string prefix = "sk-";
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const int randomPartLength = 20; // sk- + 20位 = 23位总长度

        using var rng = RandomNumberGenerator.Create();
        var randomPart = new char[randomPartLength];

        // 生成随机字符
        var randomBytes = new byte[randomPartLength];
        rng.GetBytes(randomBytes);

        for (int i = 0; i < randomPartLength; i++)
        {
            randomPart[i] = chars[randomBytes[i] % chars.Length];
        }

        return prefix + new string(randomPart);
    }

    /// <summary>
    /// 添加绑定关系
    /// </summary>
    private async Task AddBindingsAsync(
        ApiKey apiKey,
        List<(ProviderPlatform Platform, Guid GroupId)> bindings)
    {
        if (bindings == null || !bindings.Any())
            return;

        // 校验平台唯一性
        var duplicatePlatforms = bindings
            .GroupBy(b => b.Platform)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatePlatforms.Any())
        {
            throw new BadRequestException($"每个平台只能绑定一个分组。重复平台: {string.Join(", ", duplicatePlatforms)}");
        }

        foreach (var binding in bindings)
        {
            apiKey.Bindings.Add(new ApiKeyProviderGroupBinding(apiKey.Id, binding.Platform, binding.GroupId));
        }
    }

    /// <summary>
    /// 更新绑定关系
    /// </summary>
    private async Task UpdateBindingsAsync(
        ApiKey apiKey,
        List<(ProviderPlatform Platform, Guid GroupId)> bindings,
        CancellationToken cancellationToken)
    {
        // 校验平台唯一性
        var duplicatePlatforms = bindings
            .GroupBy(b => b.Platform)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatePlatforms.Any())
        {
            throw new BadRequestException($"每个平台只能绑定一个分组。重复平台: {string.Join(", ", duplicatePlatforms)}");
        }

        // 1. 找出需要删除的绑定 (只需检查 Platform)
        var bindingsToRemove = apiKey.Bindings
            .Where(existing =>
                !bindings.Any(newBinding => newBinding.Platform == existing.Platform))
            .ToList();

        if (bindingsToRemove.Any())
        {
            await bindingRepository.DeleteManyAsync(bindingsToRemove, cancellationToken);
            foreach (var binding in bindingsToRemove)
            {
                apiKey.Bindings.Remove(binding);
            }
        }

        // 2. 找出需要添加或更新的绑定
        foreach (var (platform, groupId) in bindings)
        {
            var existingBinding = apiKey.Bindings.FirstOrDefault(b => b.Platform == platform);
            if (existingBinding != null)
            {
                // 如果已存在该平台的绑定，检查是否需要更新分组
                if (existingBinding.ProviderGroupId != groupId)
                {
                    existingBinding.UpdateGroup(groupId);
                }
            }
            else
            {
                // 添加新绑定
                var newBinding = new ApiKeyProviderGroupBinding(apiKey.Id, platform, groupId);
                apiKey.Bindings.Add(newBinding);
                // 显式插入以确保 EF Core 将其标记为 Added，防止带有初值的 Guid 被误认为是已存在的数据
                await bindingRepository.InsertAsync(newBinding, cancellationToken);
            }
        }
    }
}
