using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.ObjectMapping.Core;
using Leistd.ObjectMapping.Core.Extensions;

namespace Leistd.Ddd.Application.Contracts.Extensions;

/// <summary>
/// 应用层对象映射器扩展方法
/// </summary>
public static class ObjectMapperExtensions
{
    /// <summary>
    /// 分页映射
    /// </summary>
    public static PagedResultDto<TDestination> MapPagedResult<TSource, TDestination>(
        this IObjectMapper mapper,
        PagedResultDto<TSource> pagedSource)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(pagedSource);

        var mappedItems = mapper.MapList<TSource, TDestination>(pagedSource.Items);
        return new PagedResultDto<TDestination>(pagedSource.TotalCount, mappedItems);
    }
}
