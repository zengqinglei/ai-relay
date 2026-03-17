namespace Leistd.Ddd.Application.Contracts.Dtos;

/// <summary>
/// 分页查询请求
/// </summary>
public record PagedRequestDto
{
    protected const int DefaultLimit = 10;

    public int Offset { get; init; } = 0;

    public int Limit { get; init; } = DefaultLimit;

    public string? Sorting { get; init; }
}
