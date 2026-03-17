namespace Leistd.ObjectMapping.Core.Extensions;

/// <summary>
/// 对象映射器扩展方法
/// </summary>
public static class ObjectMapperExtensions
{
    /// <summary>
    /// 批量映射
    /// </summary>
    public static List<TDestination> MapList<TSource, TDestination>(
        this IObjectMapper mapper,
        IEnumerable<TSource> sources)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(sources);

        return sources.Select(mapper.Map<TSource, TDestination>).ToList();
    }
}
