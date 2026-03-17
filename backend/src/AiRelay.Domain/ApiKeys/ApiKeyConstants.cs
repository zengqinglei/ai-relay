namespace AiRelay.Domain.ApiKeys;

/// <summary>
/// API Key 相关常量
/// </summary>
public static class ApiKeyConstants
{
    /// <summary>
    /// 密钥前缀
    /// </summary>
    public const string Prefix = "ar_";

    /// <summary>
    /// 密钥长度（字节）
    /// </summary>
    public const int KeyLengthBytes = 32;

    /// <summary>
    /// 缓存键前缀
    /// </summary>
    public const string CacheLookupPrefix = "apikey:lookup:";

    /// <summary>
    /// 缓存过期时间（小时）
    /// </summary>
    public const int CacheExpirationHours = 1;
}
