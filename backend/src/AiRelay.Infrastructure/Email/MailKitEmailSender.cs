using AiRelay.Domain.Shared.Email;
using AiRelay.Domain.Shared.Email.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace AiRelay.Infrastructure.Email;

public class MailKitEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            message.Body = new TextPart(TextFormat.Html)
            {
                Text = htmlBody
            };

            using var client = new SmtpClient();

            // For demo/development without actual SMTP configuration
            if (string.IsNullOrWhiteSpace(_options.Host) || _options.Host == "smtp.example.com")
            {
                logger.LogWarning("SMTP Host is not configured or using default example. Skipping real email sending to {To}", to);
                logger.LogInformation("EMAIL CONTENT:\nSubject: {Subject}\nBody: {Body}", subject, htmlBody);
                return;
            }

            var secureSocketOptions = _options.EnableSsl
                ? _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Email successfully sent to {To} with subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while sending email to {To}", to);
            throw; // Let the caller handle it or log it
        }
    }
}
