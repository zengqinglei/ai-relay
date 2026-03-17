namespace Leistd.Tracing.Core.Services;

public interface ICorrelationIdProvider
{
    /// <summary>
    /// 获取当前 TraceId
    /// </summary>
    string? Get();

    /// <summary>
    /// 创建新的 TraceId (32位 UUID 无横杠)
    /// </summary>
    string Create();

    /// <summary>
    /// 切换当前上下文的 TraceId
    /// </summary>
    /// <param name="correlationId">新的 TraceId</param>
    /// <returns>用于恢复上下文的 Dispose 对象</returns>
    IDisposable Change(string correlationId);
}
