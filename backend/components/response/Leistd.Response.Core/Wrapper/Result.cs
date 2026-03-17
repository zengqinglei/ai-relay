namespace Leistd.Response.Core.Wrapper;

/// <summary>
/// 统一响应结果
/// </summary>
public record Result
{
    public int Code { get; init; }
    public string? Message { get; init; }
    public string? Details { get; set; }

    public static Result Ok(string? message = null)
        => new() { Code = 0, Message = message };

    public static Result Fail(int code, string message)
        => new() { Code = code, Message = message };

    public override string ToString()
    {
        return $"Response [code={Code}, message={Message}, details={Details}]";
    }
}


