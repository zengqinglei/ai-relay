namespace Leistd.Exception.Core;

/// <summary>
/// 未知异常，默认错误码：50000
/// </summary>
public class InternalServerException(string message, System.Exception? innerException = null)
    : BusinessException("500", message, innerException);

