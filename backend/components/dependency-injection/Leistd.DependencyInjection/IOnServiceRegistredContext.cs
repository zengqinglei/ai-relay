namespace Leistd.DependencyInjection;

/// <summary>
/// 服务注册上下文接口
/// </summary>
public interface IOnServiceRegistredContext
{
    /// <summary>
    /// 服务类型
    /// </summary>
    Type ServiceType { get; }

    /// <summary>
    /// 实现类型
    /// </summary>
    Type ImplementationType { get; }

    /// <summary>
    /// 拦截器列表
    /// </summary>
    List<Type> Interceptors { get; }
}
