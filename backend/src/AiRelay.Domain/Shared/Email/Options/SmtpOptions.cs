using System.ComponentModel.DataAnnotations;

namespace AiRelay.Domain.Shared.Email.Options;

/// <summary>
/// SMTP 邮件发送配置
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.example.com";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    [Required]
    public string FromName { get; set; } = "AI Relay";

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "noreply@example.com";

    public bool EnableSsl { get; set; } = true;
}
