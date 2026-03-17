namespace AiRelay.Domain.Shared.Security.Aes;

/// <summary>
/// AES 加密服务接口
/// </summary>
public interface IAesEncryptionProvider
{
    /// <summary>
    /// 加密明文
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>加密后的密文（Base64 格式）</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// 解密密文
    /// </summary>
    /// <param name="cipherText">密文（Base64 格式）</param>
    /// <returns>解密后的明文</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// 计算确定性哈希（用于盲索引查找）
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>哈希值（Base64 格式）</returns>
    string ComputeHash(string plainText);
}
