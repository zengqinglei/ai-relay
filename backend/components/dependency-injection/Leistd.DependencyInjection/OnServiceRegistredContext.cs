namespace Leistd.DependencyInjection;

/// <summary>
/// 服务注册上下文实现
/// </summary>
public record class OnServiceRegistredContext(Type ServiceType, Type ImplementationType) : IOnServiceRegistredContext
{
    /// <summary>
    /// 拦截器列表
    /// </summary>
    public List<Type> Interceptors { get; } = [];
}

