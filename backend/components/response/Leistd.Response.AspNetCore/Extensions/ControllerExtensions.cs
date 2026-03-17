using Leistd.Response.Core.Wrapper;
using Microsoft.AspNetCore.Mvc;

namespace Leistd.Response.AspNetCore.Extensions;

/// <summary>
/// Controller 扩展方法
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    /// 返回成功响应（带数据）
    /// </summary>
    public static IActionResult OkResult<T>(this ControllerBase controller, T data, string? message = null)
    {
        return controller.Ok(Result<T>.Ok(data, message));
    }

    /// <summary>
    /// 返回成功响应（无数据）
    /// </summary>
    public static IActionResult OkResult(this ControllerBase controller, string? message = null)
    {
        return controller.Ok(Result.Ok(message));
    }

    /// <summary>
    /// 返回错误响应
    /// </summary>
    public static IActionResult FailResult(this ControllerBase controller, int code, string message)
    {
        return controller.StatusCode(GetHttpStatusCode(code), Result.Fail(code, message));
    }

    /// <summary>
    /// 返回错误响应（带错误详情）
    /// </summary>
    public static IActionResult FailResultWithErrors(
        this ControllerBase controller,
        int code,
        string message,
        List<Dictionary<string, string>> errors)
    {
        return controller.StatusCode(GetHttpStatusCode(code), ErrorResult.Fail(code, message, errors));
    }

    private static int GetHttpStatusCode(int errorCode)
    {
        var errorCodeStr = errorCode.ToString();
        if (errorCodeStr.Length >= 3)
        {
            var httpStatusCode = int.Parse(errorCodeStr[..3]);
            if (httpStatusCode is >= 100 and < 600)
                return httpStatusCode;
        }
        return 500;
    }
}
