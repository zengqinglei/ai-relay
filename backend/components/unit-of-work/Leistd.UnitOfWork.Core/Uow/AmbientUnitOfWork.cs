namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 环境工作单元实现（基于 AsyncLocal 存储当前工作单元）
/// </summary>
public class AmbientUnitOfWork : IAmbientUnitOfWork
{
    private readonly AsyncLocal<IUnitOfWork?> _currentUow = new();

    /// <inheritdoc />
    public IUnitOfWork? Get()
    {
        return _currentUow.Value;
    }

    /// <inheritdoc />
    public void Set(IUnitOfWork? unitOfWork)
    {
        _currentUow.Value = unitOfWork;
    }
}
