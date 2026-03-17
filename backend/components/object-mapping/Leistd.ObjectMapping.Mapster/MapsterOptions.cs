using Mapster;

namespace Leistd.ObjectMapping.Mapster;

/// <summary>
/// Mapster 配置选项
/// </summary>
public class MapsterOptions
{
    /// <summary>
    /// 配置器列表
    /// </summary>
    public List<Action<TypeAdapterConfig>> Configurators { get; } = [];

    /// <summary>
    /// 是否验证映射配置
    /// </summary>
    public bool ValidateMappings { get; set; }
}
