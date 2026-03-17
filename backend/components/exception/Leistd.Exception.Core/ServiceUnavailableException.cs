namespace Leistd.Exception.Core;

/// <summary>
/// 服务不可用异常，默认错误码：50300
/// </summary>
public class ServiceUnavailableException(string message, System.Exception? innerException = null)
    : BusinessException("503", message, innerException);
