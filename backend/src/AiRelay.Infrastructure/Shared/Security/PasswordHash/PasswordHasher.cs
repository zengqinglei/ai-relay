using System.Security.Cryptography;
using AiRelay.Domain.Shared.Security.PasswordHash;
using Leistd.Exception.Core;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace AiRelay.Infrastructure.Shared.Security.PasswordHash;

/// <summary>
/// 密码哈希服务实现（使用 PBKDF2）
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 10000;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new BadRequestException("密码不能为空");

        // 生成盐值
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 生成哈希
        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize
        );

        // 组合：盐值 + 哈希值
        var hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
            throw new BadRequestException("哈希密码不能为空");
        if (string.IsNullOrEmpty(providedPassword))
            throw new BadRequestException("待验证密码不能为空");

        var hashBytes = Convert.FromBase64String(hashedPassword);

        // 提取盐值
        var salt = new byte[SaltSize];
        Array.Copy(hashBytes, 0, salt, 0, SaltSize);

        // 提取存储的哈希值
        var storedHash = new byte[HashSize];
        Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);

        // 计算提供密码的哈希值
        var providedHash = KeyDerivation.Pbkdf2(
            password: providedPassword,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize
        );

        // 比较哈希值
        return CryptographicOperations.FixedTimeEquals(storedHash, providedHash);
    }
}
