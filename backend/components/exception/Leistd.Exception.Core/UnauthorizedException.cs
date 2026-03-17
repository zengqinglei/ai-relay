namespace Leistd.Exception.Core;

/// <summary>
/// 未授权异常，默认错误码：40100
/// </summary>
public class UnauthorizedException(string message, System.Exception? innerException = null)
    : BusinessException("401", message, innerException);

