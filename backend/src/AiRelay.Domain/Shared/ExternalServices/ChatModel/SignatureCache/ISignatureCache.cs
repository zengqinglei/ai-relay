namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;

/// <summary>
/// Thinking Block 签名缓存接口
/// </summary>
/// <remarks>
/// 用于在多轮对话中传递 thoughtSignature，确保 Thinking Block 的签名链完整性。
/// 参考 Antigravity-Manager/proxy/SignatureCache 实现。
/// </remarks>
public interface ISignatureCache
{
    /// <summary>
    /// 缓存会话签名（用于 Thinking Block 签名链传递）
    /// </summary>
    /// <param name="sessionId">会话标识（由 SessionStickyStrategy 生成）</param>
    /// <param name="signature">Base64 编码的签名字符串</param>
    void CacheSignature(string sessionId, string signature);

    /// <summary>
    /// 获取会话的最新签名
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns>签名字符串，如果不存在或已过期返回 null</returns>
    string? GetSignature(string sessionId);

    /// <summary>
    /// 清理过期签名（签名有效期为 30 分钟）
    /// </summary>
    void CleanupExpiredSignatures();
}
