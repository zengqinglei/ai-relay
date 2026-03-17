namespace Leistd.Response.Core.Wrapper;

public record class ErrorResult : Result
{
    public List<Dictionary<string, string>>? Errors { get; set; }

    public static ErrorResult Fail(int code, string message, List<Dictionary<string, string>> errors)
        => new()
        {
            Code = code,
            Message = message,
            Errors = errors
        };
}
