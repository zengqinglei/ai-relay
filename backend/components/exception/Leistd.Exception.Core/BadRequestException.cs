namespace Leistd.Exception.Core;

/// <summary>
/// 普通异常，默认错误码：40000
/// </summary>
public class BadRequestException(string message, System.Exception? innerException = null)
    : BusinessException("400", message, innerException);

