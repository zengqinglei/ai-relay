namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 环境工作单元接口（用于获取/设置当前工作单元）
/// </summary>
public interface IAmbientUnitOfWork
{
    /// <summary>
    /// 获取当前工作单元
    /// </summary>
    IUnitOfWork? Get();

    /// <summary>
    /// 设置当前工作单元（设为 null 时恢复到外层工作单元）
    /// </summary>
    void Set(IUnitOfWork? value);
}
