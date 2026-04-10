using System.Security.Cryptography;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ApiKeys.Repositories;
using AiRelay.Domain.Shared.Security.Aes;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ApiKeys.DomainServices;

/// <summary>
/// ApiKey 领域服务
/// </summary>
public class ApiKeyDomainService(
    IApiKeyRepository apiKeyRepository,
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
        List<(int Priority, Guid GroupId)> bindings,
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
        List<(int Priority, Guid GroupId)> bindings,
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

        // 先保存主体，避免与绑定删除/插入混入同一 SaveChanges 批次引发并发异常
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);

        // 绑定全量更新（独立 SaveChanges）
        if (bindings != null)
        {
            await UpdateBindingsAsync(apiKey, bindings, cancellationToken);
        }
    }

    /// <summary>
    /// 删除 API Key
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 仅需加载 Bindings，供删除场景使用
        var apiKey = await apiKeyRepository.GetWithBindingsAsync(id, cancellationToken);
        if (apiKey == null)
        {
            throw new BadRequestException($"API Key 不存在: {id}");
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
        List<(int Priority, Guid GroupId)> bindings)
    {
        if (bindings == null || !bindings.Any())
            return;

        // 校验分组唯一性
        var duplicateGroups = bindings
            .GroupBy(b => b.GroupId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateGroups.Any())
        {
            throw new BadRequestException("在同一层级池中绑定了重复的分组。");
        }

        foreach (var binding in bindings)
        {
            apiKey.Bindings.Add(new ApiKeyProviderGroupBinding(apiKey.Id, binding.Priority, binding.GroupId));
        }
    }

    private async Task UpdateBindingsAsync(
        ApiKey apiKey,
        List<(int Priority, Guid GroupId)> bindings,
        CancellationToken cancellationToken)
    {
        // 校验分组唯一性
        var duplicateGroups = bindings
            .GroupBy(b => b.GroupId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateGroups.Any())
        {
            throw new BadRequestException("在同一层级池中绑定了重复的分组。");
        }

        // 1. 删除旧绑定（显式操作，独立于主体 SaveChanges）
        var existing = apiKey.Bindings.ToList();
        if (existing.Any())
        {
            await bindingRepository.DeleteManyAsync(existing, cancellationToken);
            apiKey.Bindings.Clear();
        }

        // 2. 插入新绑定
        foreach (var (priority, groupId) in bindings)
        {
            var newBinding = new ApiKeyProviderGroupBinding(apiKey.Id, priority, groupId);
            apiKey.Bindings.Add(newBinding);
            await bindingRepository.InsertAsync(newBinding, cancellationToken);
        }
    }
}
