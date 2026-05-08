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
}
