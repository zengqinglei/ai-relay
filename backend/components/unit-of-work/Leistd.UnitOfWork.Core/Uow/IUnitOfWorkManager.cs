using Leistd.UnitOfWork.Core.Options;

namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 工作单元管理器接口
/// </summary>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// 当前工作单元
    /// </summary>
    IUnitOfWork? Current { get; }

    /// <summary>
    /// 开启工作单元
    /// </summary>
    /// <param name="options">工作单元配置</param>
    /// <param name="requiresNew">是否强制创建新的工作单元</param>
    Task<IUnitOfWork> BeginAsync(UnitOfWorkOptions? options = null, bool requiresNew = true);
}
