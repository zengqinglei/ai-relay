using AutoMapper;
using AutoMapper.QueryableExtensions;
using Leistd.ObjectMapping.Core;

namespace Leistd.ObjectMapping.AutoMapper.Extensions;

/// <summary>
/// AutoMapper 特有扩展方法
/// </summary>
public static class AutoMapperExtensions
{
    /// <summary>
    /// 获取底层 AutoMapper 的 IMapper
    /// </summary>
    public static IMapper GetAutoMapper(this IObjectMapper objectMapper)
    {
        if (objectMapper is AutoMapperObjectMapper autoMapper)
        {
            return autoMapper.GetMapper();
        }

        throw new InvalidOperationException(
            $"当前 IObjectMapper 实现不是 AutoMapperObjectMapper，无法获取底层 IMapper。实际类型：{objectMapper.GetType().FullName}");
    }

    /// <summary>
    /// LINQ 投影映射（将映射下推到数据库查询）
    /// </summary>
    public static IQueryable<TDestination> ProjectTo<TSource, TDestination>(
        this IQueryable<TSource> queryable,
        IObjectMapper objectMapper)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(objectMapper);

        var mapper = objectMapper.GetAutoMapper();
        return queryable.ProjectTo<TDestination>(mapper.ConfigurationProvider);
    }
}
