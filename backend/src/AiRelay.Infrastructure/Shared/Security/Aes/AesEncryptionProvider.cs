using System.Security.Cryptography;
using System.Text;
using AiRelay.Domain.Shared.Security.Aes;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using Microsoft.Extensions.Options;

namespace AiRelay.Infrastructure.Shared.Security.Aes;

/// <summary>
/// AES 加密服务实现
/// </summary>
public class AesEncryptionProvider : IAesEncryptionProvider
{
    private readonly byte[] _key;

    public AesEncryptionProvider(IOptions<JwtOptions> jwtOptions)
    {
        var secretKey = jwtOptions.Value.SecretKey
            ?? throw new InvalidOperationException("JWT SecretKey 未配置");

        // 使用 SHA256 将密钥转换为 32 字节（AES-256）
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
    }

    /// <summary>
    /// 加密明文
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // 每次生成新的 IV

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        var encrypted = msEncrypt.ToArray();

        // 将 IV 和密文合并：[IV(16字节)][密文]
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密密文
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));

        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = _key;

        // 提取 IV（前 16 字节）
        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;

        // 提取密文（剩余字节）
        var cipher = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(cipher);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    /// <summary>
    /// 计算确定性哈希（用于盲索引查找）
    /// </summary>
    public string ComputeHash(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        // 使用 HMACSHA256 确保哈希的安全性（防止彩虹表攻击）
        // 复用 _key 作为 HMAC 的密钥
        using var hmac = new HMACSHA256(_key);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainText));

        // 返回 Base64 字符串作为索引
        return Convert.ToBase64String(hashBytes);
    }
}
