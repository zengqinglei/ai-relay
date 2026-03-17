using Leistd.ObjectMapping.Core;
using Mapster;
using MapsterMapper;

namespace Leistd.ObjectMapping.Mapster;

/// <summary>
/// Mapster 对象映射器实现
/// </summary>
public class MapsterObjectMapper(IMapper mapper) : IObjectMapper
{
    public TDestination Map<TDestination>(object source)
    {
        return mapper.Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return mapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, IDictionary<string, object> contextItems)
    {
        using (new MapContextScope())
        {
            if (contextItems != null && MapContext.Current != null)
            {
                foreach (var item in contextItems)
                {
                    MapContext.Current.Parameters[item.Key] = item.Value;
                }
            }
            return mapper.Map<TSource, TDestination>(source);
        }
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return mapper.Map(source, destination);
    }
}
