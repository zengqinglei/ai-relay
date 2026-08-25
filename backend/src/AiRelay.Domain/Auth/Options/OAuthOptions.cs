namespace AiRelay.Domain.Auth.Options;

public class OAuthOptions
{
    public const string SectionName = "OAuth";



    /// <summary>
    /// Cookie 会话过期天数（默认7天）
    /// </summary>
    public int CookieExpireDays { get; set; } = 7;

    public bool UseDevelopmentCertificates { get; set; } = true;

    public string? SigningCertificatePath { get; set; }

    public string? SigningCertificatePassword { get; set; }

    public string? EncryptionCertificatePath { get; set; }

    public string? EncryptionCertificatePassword { get; set; }

    /// <summary>
    /// 是否禁用 HTTPS 要求（内网 S2S 调用时设为 true）
    /// </summary>
    public bool DisableHttpsRequirement { get; set; } = false;

    /// <summary>
    /// OpenIddict 签发 token 使用的 issuer URL（外部可访问的域名）。
    /// 为空时使用请求的 host。跨服务 token 验证场景必须设置。
    /// </summary>
    public string? Issuer { get; set; }
}
