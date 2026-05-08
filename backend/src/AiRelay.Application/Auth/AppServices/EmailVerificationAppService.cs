using System.Security.Cryptography;
using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Shared.Email;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Users.Options;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.Auth.AppServices;

public class EmailVerificationAppService(
    IDistributedCache distributedCache,
    IOptions<UserRegistrationOptions> options,
    ICaptchaAppService captchaAppService,
    IEmailSender emailSender,
    IRepository<User, Guid> userRepository) : BaseAppService, IEmailVerificationAppService
{
    private readonly UserRegistrationOptions _options = options.Value;

    public async Task SendEmailCodeAsync(SendEmailCodeInputDto input, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(input.Email);
        var isValidCaptcha = await captchaAppService.ValidateCaptchaAsync(input.CaptchaToken, input.CaptchaCode, cancellationToken);
        if (!isValidCaptcha)
        {
            throw new BadRequestException("图形验证码不正确或已过期");
        }

        var existingUser = await userRepository.GetFirstAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken: cancellationToken);
        if (existingUser != null)
        {
            throw new BadRequestException("邮箱已被使用");
        }

        var limitKey = GetLimitCacheKey(normalizedEmail);
        var isLimited = await distributedCache.GetStringAsync(limitKey, cancellationToken);
        if (!string.IsNullOrEmpty(isLimited))
        {
            throw new BadRequestException("发送过于频繁，请稍后再试");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var subject = "【AI Relay】账号注册验证码";
        var htmlBody = $@"
<div style='font-family: Arial, sans-serif; max-w-md: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
    <div style='background-color: #0f172a; padding: 20px; text-align: center; color: white;'>
        <h2 style='margin: 0;'>AI Relay 验证码</h2>
    </div>
    <div style='padding: 30px; background-color: #f8fafc; color: #334155;'>
        <p>您好，</p>
        <p>您正在进行账号注册，您的验证码是：</p>
        <div style='margin: 20px 0; text-align: center;'>
            <span style='font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #2563eb;'>{code}</span>
        </div>
        <p style='font-size: 14px; color: #64748b;'>此验证码将在 {_options.EmailCodeExpiryMinutes} 分钟后过期。请勿向他人泄露此验证码。</p>
        <p style='font-size: 14px; color: #64748b; margin-top: 30px;'>如果这不是您的操作，请忽略此邮件。</p>
    </div>
</div>";
        var codeCacheKey = GetCodeCacheKey(normalizedEmail);
        await distributedCache.SetStringAsync(codeCacheKey, code, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.EmailCodeExpiryMinutes)
        }, cancellationToken);

        try
        {
            await emailSender.SendAsync(normalizedEmail, subject, htmlBody, cancellationToken);
        }
        catch
        {
            await distributedCache.RemoveAsync(codeCacheKey, cancellationToken);
            throw;
        }

        await distributedCache.SetStringAsync(limitKey, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.EmailCodeSendIntervalSeconds)
        }, cancellationToken);
    }

    public async Task<bool> ValidateEmailCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return false;

        var codeCacheKey = GetCodeCacheKey(NormalizeEmail(email));
        var cachedCode = await distributedCache.GetStringAsync(codeCacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cachedCode) || !cachedCode.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        await distributedCache.RemoveAsync(codeCacheKey, cancellationToken);
        return true;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string GetCodeCacheKey(string email) => $"AiRelay:EmailCode:{email}";
    private static string GetLimitCacheKey(string email) => $"AiRelay:EmailCodeLimit:{email}";
}
