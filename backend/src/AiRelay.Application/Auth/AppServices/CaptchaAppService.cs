using System.Security.Cryptography;
using System.Text;
using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.Options;
using Leistd.Ddd.Application.AppService;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.Auth.AppServices;

public class CaptchaAppService(
    IDistributedCache distributedCache,
    IOptions<UserRegistrationOptions> options) : BaseAppService, ICaptchaAppService
{
    private const string CaptchaLetters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string CaptchaDigits = "23456789";
    private const string CaptchaCharacters = CaptchaLetters + CaptchaDigits;
    private readonly UserRegistrationOptions _options = options.Value;
    private static readonly string[] BgColors = ["#f0fdf4", "#f8fafc", "#fffbeb", "#fef2f2", "#f0f9ff"];

    public async Task<CaptchaOutputDto> GenerateCaptchaAsync(CancellationToken cancellationToken = default)
    {
        var code = GenerateCode(4);
        var token = Guid.NewGuid().ToString("N");

        var bg = BgColors[RandomNumberGenerator.GetInt32(BgColors.Length)];
        var lineY = RandomNumberGenerator.GetInt32(5, 35);
        var angle = RandomNumberGenerator.GetInt32(-15, 15);

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"130\" height=\"44\"><rect width=\"100%\" height=\"100%\" fill=\"{bg}\"/><line x1=\"0\" y1=\"{lineY}\" x2=\"130\" y2=\"{44 - lineY}\" stroke=\"#94a3b8\" stroke-width=\"2\" opacity=\"0.6\"/><text x=\"50%\" y=\"50%\" font-size=\"24\" font-family=\"monospace\" fill=\"#0f172a\" font-weight=\"bold\" font-style=\"italic\" letter-spacing=\"6\" dominant-baseline=\"central\" text-anchor=\"middle\" transform=\"rotate({angle}, 65, 22)\">{string.Join(" ", code.ToCharArray())}</text></svg>";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        var imageBase64 = $"data:image/svg+xml;base64,{base64}";

        // 3. 保存到缓存
        var cacheKey = GetCacheKey(token);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CaptchaExpiryMinutes)
        };
        await distributedCache.SetStringAsync(cacheKey, code, cacheOptions, cancellationToken);

        return new CaptchaOutputDto
        {
            CaptchaToken = token,
            CaptchaImageBase64 = imageBase64
        };
    }

    public async Task<bool> ValidateCaptchaAsync(string token, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(code))
            return false;

        var cacheKey = GetCacheKey(token);
        var cachedCode = await distributedCache.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cachedCode))
            return false;

        // 验证码只能使用一次，验证后立即删除
        await distributedCache.RemoveAsync(cacheKey, cancellationToken);

        return cachedCode.Equals(code, StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateCode(int length)
    {
        var chars = new char[length];
        chars[0] = CaptchaLetters[RandomNumberGenerator.GetInt32(CaptchaLetters.Length)];
        chars[1] = CaptchaDigits[RandomNumberGenerator.GetInt32(CaptchaDigits.Length)];

        for (var i = 2; i < chars.Length; i++)
        {
            chars[i] = CaptchaCharacters[RandomNumberGenerator.GetInt32(CaptchaCharacters.Length)];
        }

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static string GetCacheKey(string token) => $"AiRelay:Captcha:{token}";
}
