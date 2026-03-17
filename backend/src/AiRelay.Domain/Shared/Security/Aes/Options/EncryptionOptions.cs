using AiRelay.Domain.Shared.Security.Aes.Constants;

namespace AiRelay.Domain.Shared.Security.Aes.Options;

/// <summary>
/// 加密配置选项
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Encryption";

    /// <summary>
    /// 加密密钥（Base64 或 Hex 格式）
    /// 如果未配置，将自动生成（仅适用于开发环境）
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 获取加密密钥字节数组
    /// </summary>
    public byte[] GetKeyBytes()
    {
        // 如果未配置密钥，生成一个默认的（仅用于开发环境）
        if (string.IsNullOrEmpty(Key))
        {
            return GenerateDefaultKey();
        }

        // 尝试 Base64 解码
        try
        {
            var keyBytes = Convert.FromBase64String(Key);
            if (keyBytes.Length == AesConstants.AesKeyLengthBytes)
                return keyBytes;
        }
        catch
        {
            // Base64 解码失败，尝试十六进制
        }

        // 尝试十六进制解码
        try
        {
            if (Key.Length == AesConstants.AesKeyLengthBytes * 2)
            {
                var keyBytes = Convert.FromHexString(Key);
                if (keyBytes.Length == AesConstants.AesKeyLengthBytes)
                    return keyBytes;
            }
        }
        catch
        {
            // 十六进制解码失败
        }

        throw new InvalidOperationException(
            "Encryption key must be either a Base64-encoded 32-byte key or a 64-character hexadecimal string.");
    }

    /// <summary>
    /// 生成默认密钥（仅用于开发环境）
    /// ⚠️ 生产环境必须配置 ENCRYPTION_KEY
    /// </summary>
    private static byte[] GenerateDefaultKey()
    {
        // 使用固定种子生成确定性密钥，以便开发环境重启后仍能解密之前的数据
        // ⚠️ 这仅用于开发环境，生产环境必须配置真实密钥
        var deterministicKey = new byte[AesConstants.AesKeyLengthBytes];
        var rng = new Random(42); // 固定种子
        rng.NextBytes(deterministicKey);
        return deterministicKey;
    }
}
