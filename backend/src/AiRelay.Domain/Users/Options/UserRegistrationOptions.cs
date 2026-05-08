using System.ComponentModel.DataAnnotations;

namespace AiRelay.Domain.Users.Options;

/// <summary>
/// 用户注册相关配置（支持后台动态配置管理）
/// </summary>
public class UserRegistrationOptions
{
    public const string SectionName = "UserRegistration";

    /// <summary>
    /// 是否开启邮箱验证（若不开启则只验证图形验证码）
    /// </summary>
    public bool EnableEmailVerification { get; set; } = false;

    /// <summary>
    /// 图形验证码有效期（分钟）
    /// </summary>
    [Range(1, 60)]
    public int CaptchaExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// 邮箱验证码有效期（分钟）
    /// </summary>
    [Range(1, 60)]
    public int EmailCodeExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// 发送邮箱验证码频率限制（秒）
    /// </summary>
    [Range(1, 3600)]
    public int EmailCodeSendIntervalSeconds { get; set; } = 60;
}
