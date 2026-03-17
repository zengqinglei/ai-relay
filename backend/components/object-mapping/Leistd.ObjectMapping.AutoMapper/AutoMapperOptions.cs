using AutoMapper;

namespace Leistd.ObjectMapping.AutoMapper;

/// <summary>
/// AutoMapper 配置选项
/// </summary>
public class AutoMapperOptions
{
    /// <summary>
    /// 配置委托列表
    /// </summary>
    public List<Action<IMapperConfigurationExpression>> Configurators { get; } = new();

    /// <summary>
    /// 是否验证映射配置
    /// </summary>
    public bool ValidateMappings { get; set; } = false;
}
