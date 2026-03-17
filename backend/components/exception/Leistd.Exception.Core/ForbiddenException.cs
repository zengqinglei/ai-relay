namespace Leistd.Exception.Core;

/// <summary>
/// 禁止访问异常，默认错误码：40300
/// </summary>
public class ForbiddenException(string message, System.Exception? innerException = null)
    : BusinessException("403", message, innerException);

