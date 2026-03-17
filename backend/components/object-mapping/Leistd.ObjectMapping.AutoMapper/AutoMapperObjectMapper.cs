using AutoMapper;
using Leistd.ObjectMapping.Core;

namespace Leistd.ObjectMapping.AutoMapper;

/// <summary>
/// AutoMapper 实现的对象映射器
/// </summary>
public class AutoMapperObjectMapper(IMapper mapper) : IObjectMapper
{
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return mapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, IDictionary<string, object> contextItems)
    {
        return mapper.Map<TSource, TDestination>(source, opt =>
        {
            foreach (var item in contextItems)
            {
                opt.Items[item.Key] = item.Value;
            }
        });
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return mapper.Map(source, destination);
    }

    /// <summary>
    /// 获取底层 IMapper（用于 ProjectTo）
    /// </summary>
    public IMapper GetMapper() => mapper;
}
