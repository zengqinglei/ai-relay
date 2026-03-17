namespace Leistd.Response.Core.Wrapper;

/// <summary>
/// 带数据的响应结果
/// </summary>
public record Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(T data, string? message = null)
        => new() { Code = 0, Data = data, Message = message };

    public new static Result<T> Fail(int code, string? message = null)
        => new() { Code = code, Message = message };
}
