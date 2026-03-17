namespace Leistd.DependencyInjection;

/// <summary>
/// 服务注册回调列表
/// </summary>
public class ServiceRegistrationActionList : List<Action<IOnServiceRegistredContext>>
{
}
