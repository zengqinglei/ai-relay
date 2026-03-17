namespace AiRelay.Domain.UsageRecords.Providers;

public interface IPricingProvider
{
    /// <summary>
    /// 根据模型名称获取定价信息（输入/输出/缓存单价）
    /// </summary>
    /// <returns>如果没有找到定价，返回 null</returns>
    Task<ModelPricingInfo?> GetPricingAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新定价缓存（从远程 URL 或本地备份加载）
    /// </summary>
    Task UpdatePricingCacheAsync(CancellationToken cancellationToken);
}

public record ModelPricingInfo(
    decimal InputPrice,
    decimal OutputPrice,
    decimal CacheReadPrice,
    decimal CacheCreationPrice,
    int? LongContextInputThreshold = null,
    decimal? LongContextInputMultiplier = null,
    decimal? LongContextOutputMultiplier = null
);
