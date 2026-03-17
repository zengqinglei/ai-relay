namespace AiRelay.Application.Initialization;

/// <summary>
/// 系统初始化器接口
/// 负责初始化系统角色、用户、权限等基础数据
/// </summary>
public interface ISystemInitializer
{
    /// <summary>
    /// 初始化系统基础数据
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
