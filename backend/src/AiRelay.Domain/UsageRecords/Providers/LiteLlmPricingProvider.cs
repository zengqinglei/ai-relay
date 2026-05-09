using System.Text.Json;
using AiRelay.Domain.Shared.Json;
using AiRelay.Domain.UsageRecords.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Domain.UsageRecords.Providers;

public class LiteLlmPricingProvider(
    IOptions<ModelPricingOptions> pricingOptions,
    IMemoryCache cache,
    IHttpClientFactory httpClientFactory,
    ILogger<LiteLlmPricingProvider> logger) : IPricingProvider
{
    private readonly ModelPricingOptions _pricingOptions = pricingOptions.Value;
    private const string CacheKey = "LiteLlmPricingData";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(25); // 略长于 24 小时更新间隔
    private static readonly SemaphoreSlim UpdateLock = new(1, 1); // 防止并发更新 

    public async Task<ModelPricingInfo?> GetPricingAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var pricingData = await GetPricingDataAsync(cancellationToken);
        if (pricingData == null) return null;

        // 1. 精确匹配
        if (pricingData.TryGetValue(modelName, out var info))
        {
            return info;
        }

        // 2. 忽略大小写和连字符匹配
        // "gpt-4-turbo" vs "GPT4Turbo"
        // 查找 key 中去掉 -_ 后与 input 去掉 -_ 后一致的
        var normalizedInput = NormalizeModelName(modelName);
        foreach (var key in pricingData.Keys)
        {
            if (NormalizeModelName(key) == normalizedInput)
            {
                return pricingData[key];
            }
        }

        // 3. Bedrock/Vertex 前缀处理 (e.g. "us.anthropic.claude-3-sonnet-..." -> "anthropic.claude-3-sonnet-...")
        if (modelName.Contains('.'))
        {
            var parts = modelName.Split('.');
            if (parts.Length > 1)
            {
                var withoutRegion = string.Join(".", parts.Skip(1));
                if (pricingData.TryGetValue(withoutRegion, out info)) return info;
            }
        }

        // 4. 短模型名匹配 provider/model key，例如 "glm-5" -> "openrouter/z-ai/glm-5"。
        var shortNameMatch = pricingData.Keys
            .Where(k => NormalizeModelName(GetModelNamePart(k)) == normalizedInput)
            .OrderBy(k => k.Length)
            .FirstOrDefault();

        if (shortNameMatch != null)
        {
            logger.LogInformation("通过短模型名匹配找到模型定价: {Input} -> {Match}", modelName, shortNameMatch);
            return pricingData[shortNameMatch];
        }

        // 5. 模糊匹配 (前缀匹配)
        var bestMatch = pricingData.Keys
            .Where(k => modelName.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(k => k.Length)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            return pricingData[bestMatch];
        }

        // 6. 跨平台/提供商名称后置匹配
        if (modelName.Contains('/'))
        {
            var modelPart = GetModelNamePart(modelName);
            var normalizedModelPart = NormalizeModelName(modelPart);

            var suffixMatch = pricingData.Keys
                .Where(k => NormalizeModelName(k).EndsWith(normalizedModelPart, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Length)
                .FirstOrDefault();

            if (suffixMatch != null)
            {
                logger.LogInformation("通过后缀匹配找到模型定价: {Input} -> {Match}", modelName, suffixMatch);
                return pricingData[suffixMatch];
            }
        }

        logger.LogWarning("未找到模型定价: {ModelName}", modelName);
        return null;
    }

    private string NormalizeModelName(string name)
    {
        var cleanName = name.Replace("us.", "").Replace("eu.", "").Replace("apac.", "").Replace("anthropic.", "");
        
        var span = cleanName.AsSpan();
        var sb = new System.Text.StringBuilder(span.Length);
        foreach (var c in span)
        {
            if (c is not ('-' or '_' or '.' or ':' or '/'))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static string GetModelNamePart(string name)
    {
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? name : parts[^1];
    }

    public async Task UpdatePricingCacheAsync(CancellationToken cancellationToken)
    {
        var url = _pricingOptions.RemoteUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("未配置 Pricing:RemoteUrl，尝试使用本地备份");
            await LoadLocalFileAsync(cancellationToken);
            return;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var json = await client.GetStringAsync(url, cancellationToken);

            await ParseAndCachePricingDataAsync(json);
            logger.LogInformation("从远程 URL 更新模型价格表成功");

            // 同步写入本地备份文件，保持 fallback 数据最新
            await SaveLocalFileAsync(json, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "从远程 URL 更新模型价格表失败，尝试使用本地备份");
            await LoadLocalFileAsync(cancellationToken);
        }
    }

    private async Task SaveLocalFileAsync(string json, CancellationToken cancellationToken)
    {
        var path = _pricingOptions.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                var existing = await File.ReadAllTextAsync(path, cancellationToken);
                if (existing == json)
                {
                    logger.LogDebug("本地备份文件内容无变化，跳过写入");
                    return;
                }
            }

            await File.WriteAllTextAsync(path, json, cancellationToken);
            logger.LogDebug("本地备份文件已更新: {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "写入本地备份文件失败，不影响缓存更新");
        }
    }

    private async Task LoadLocalFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = _pricingOptions.LocalPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                logger.LogError("本地备份文件不存在: {Path}", path);
                return;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            await ParseAndCachePricingDataAsync(json);
            logger.LogInformation("从本地备份加载模型价格表成功: {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "从本地备份加载模型价格表失败");
        }
    }

    private Task ParseAndCachePricingDataAsync(string json)
    {
        var rawData = JsonSerializer.Deserialize<Dictionary<string, LiteLlmModelEntry>>(json, JsonOptions.SnakeCase);

        if (rawData != null)
        {
            var pricingMap = new Dictionary<string, ModelPricingInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in rawData)
            {
                var tier = value.TieredPricing?.OrderBy(x => x.Range?.FirstOrDefault() ?? 0).FirstOrDefault();
                pricingMap[key] = new ModelPricingInfo(
                    (decimal)(value.InputCostPerToken != 0 ? value.InputCostPerToken : tier?.InputCostPerToken ?? 0),
                    (decimal)(value.OutputCostPerToken != 0 ? value.OutputCostPerToken : tier?.OutputCostPerToken ?? 0),
                    (decimal)value.CacheReadInputTokenCost,
                    (decimal)value.CacheCreationInputTokenCost,
                    value.LongContextInputThreshold,
                    value.LongContextInputMultiplier.HasValue ? (decimal)value.LongContextInputMultiplier.Value : null,
                    value.LongContextOutputMultiplier.HasValue ? (decimal)value.LongContextOutputMultiplier.Value : null
                );
            }

            cache.Set(CacheKey, pricingMap, CacheDuration);
            logger.LogInformation("模型价格表已缓存，包含 {Count} 个模型", pricingMap.Count);
        }

        return Task.CompletedTask;
    }

    private async Task<Dictionary<string, ModelPricingInfo>?> GetPricingDataAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out Dictionary<string, ModelPricingInfo>? data))
        {
            return data;
        }

        await UpdateLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查：可能在等待锁期间已被其他线程更新
            if (cache.TryGetValue(CacheKey, out data))
            {
                return data;
            }

            await UpdatePricingCacheAsync(cancellationToken);
            cache.TryGetValue(CacheKey, out data);
            return data;
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    private class LiteLlmModelEntry
    {
        public double InputCostPerToken { get; set; }
        public double OutputCostPerToken { get; set; }
        public double CacheReadInputTokenCost { get; set; }
        public double CacheCreationInputTokenCost { get; set; }
        public int? LongContextInputThreshold { get; set; }
        public double? LongContextInputMultiplier { get; set; }
        public double? LongContextOutputMultiplier { get; set; }
        public List<LiteLlmTieredPricingEntry>? TieredPricing { get; set; }
    }

    private class LiteLlmTieredPricingEntry
    {
        public double InputCostPerToken { get; set; }
        public double OutputCostPerToken { get; set; }
        public List<double>? Range { get; set; }
    }
}
