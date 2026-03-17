namespace Leistd.ObjectMapping.Core;

/// <summary>
/// 对象映射器接口
/// </summary>
public interface IObjectMapper
{
    /// <summary>
    /// 映射对象（创建新实例）
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// 映射对象（带上下文数据）
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, IDictionary<string, object> contextItems);

    /// <summary>
    /// 映射到现有对象（更新现有实例）
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}
