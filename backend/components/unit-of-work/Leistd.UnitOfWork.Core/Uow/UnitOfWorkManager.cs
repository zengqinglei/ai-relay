using Leistd.UnitOfWork.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 工作单元管理器实现
/// </summary>
public class UnitOfWorkManager(
    UnitOfWorkOptions defaultUowOptions,
    IServiceProvider serviceProvider,
    IAmbientUnitOfWork ambientUnitOfWork,
    ILogger<UnitOfWorkManager>? logger = null) : IUnitOfWorkManager
{
    /// <inheritdoc />
    public IUnitOfWork? Current => GetCurrentUnitOfWork();

    /// <inheritdoc />
    public Task<IUnitOfWork> BeginAsync(UnitOfWorkOptions? options = null, bool requiresNew = true)
    {
        if (options == null && requiresNew)
        {
            options = defaultUowOptions.Clone();
            options.IsTransactional = true;
        }

        var currentUow = GetCurrentUnitOfWork();
        if (currentUow != null && !requiresNew)
        {
            logger?.LogDebug("复用现有工作单元 {UowId}（创建子工作单元）", currentUow.Id);
            return Task.FromResult<IUnitOfWork>(new ChildUnitOfWork(currentUow));
        }

        var unitOfWork = CreateNewUnitOfWork();
        unitOfWork.Initialize(options!);

        logger?.LogDebug("创建新工作单元 {UowId}", unitOfWork.Id);

        return Task.FromResult(unitOfWork);
    }

    /// <summary>
    /// 获取当前有效的工作单元（跳过已释放或已完成的）
    /// </summary>
    private IUnitOfWork? GetCurrentUnitOfWork()
    {
        var uow = ambientUnitOfWork.Get();

        while (uow != null && (uow.IsDisposed || uow.IsCompleted))
        {
            uow = uow.Outer;
        }

        return uow;
    }

    /// <summary>
    /// 创建新的工作单元实例
    /// </summary>
    private IUnitOfWork CreateNewUnitOfWork()
    {
        var scope = serviceProvider.CreateScope();
        try
        {
            var outerUow = ambientUnitOfWork.Get();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            unitOfWork.SetOuter(outerUow);
            ambientUnitOfWork.Set(unitOfWork);

            // 订阅 Disposed 事件，用于恢复外层工作单元
            if (unitOfWork is UnitOfWork concreteUow)
            {
                concreteUow.Disposed += (sender, args) =>
                {
                    ambientUnitOfWork.Set(outerUow);
                    scope.Dispose();
                };
            }
            else if (unitOfWork is ChildUnitOfWork childUow)
            {
                childUow.Disposed += (sender, args) =>
                {
                    ambientUnitOfWork.Set(outerUow);
                    scope.Dispose();
                };
            }

            return unitOfWork;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
