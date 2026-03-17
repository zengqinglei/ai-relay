namespace Leistd.Exception.Core;

/// <summary>
/// 未找到异常，默认错误码：40400
/// </summary>
public class NotFoundException(string message, System.Exception? innerException = null)
    : BusinessException("404", message, innerException);

