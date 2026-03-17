namespace Leistd.Exception.Core;

/// <summary>
/// 不支持媒体数据异常，默认错误码：41500
/// </summary>
public class UnsupportedMediaTypeException(string message, System.Exception? innerException = null)
    : BusinessException("415", message, innerException);

