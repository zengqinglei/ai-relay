namespace AiRelay.Domain.Auth.Options;

/// <summary>
/// 外部身份验证配置选项
/// </summary>
public class ExternalAuthOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ExternalAuth";

    /// <summary>
    /// GitHub 配置
    /// </summary>
    public ProviderConfig? Github { get; set; }

    /// <summary>
    /// Google 配置
    /// </summary>
    public ProviderConfig? Google { get; set; }

    /// <summary>
    /// 提供商配置
    /// </summary>
    public class ProviderConfig
    {
        /// <summary>
        /// 重定向 URI
        /// </summary>
        public string? RedirectUri { get; set; }
    }

    /// <summary>
    /// 根据提供商名称获取配置
    /// </summary>
    public ProviderConfig? GetProviderConfig(string provider)
    {
        return provider.ToLower() switch
        {
            "github" => Github,
            "google" => Google,
            _ => null
        };
    }
}
