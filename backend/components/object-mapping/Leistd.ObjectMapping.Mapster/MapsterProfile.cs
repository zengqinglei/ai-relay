using Mapster;

namespace Leistd.ObjectMapping.Mapster;

/// <summary>
/// Mapster 映射配置基类（类似 AutoMapper 的 Profile）
/// </summary>
public abstract class MapsterProfile
{
    protected TypeAdapterConfig Config { get; private set; } = null!;

    /// <summary>
    /// 配置映射
    /// </summary>
    public void Configure(TypeAdapterConfig config)
    {
        Config = config;
        ConfigureMappings();
    }

    /// <summary>
    /// 配置映射规则（子类重写此方法）
    /// </summary>
    protected abstract void ConfigureMappings();

    /// <summary>
    /// 创建映射
    /// </summary>
    protected TypeAdapterSetter<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        return Config.NewConfig<TSource, TDestination>();
    }
}
