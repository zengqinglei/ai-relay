using System.Collections.ObjectModel;

namespace Leistd.Ddd.Application.Contracts.Dtos;

/// <summary>
/// 分页结果
/// </summary>
public record PagedResultDto<T>(long TotalCount, IReadOnlyList<T> Items)
{
    public PagedResultDto(long totalCount, IEnumerable<T> items)
        : this(totalCount, items?.ToList().AsReadOnly() ?? ReadOnlyCollection<T>.Empty)
    {
    }
}
