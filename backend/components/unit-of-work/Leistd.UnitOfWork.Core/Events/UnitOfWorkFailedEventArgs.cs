using Leistd.UnitOfWork.Core.Uow;

namespace Leistd.UnitOfWork.Core.Events;

/// <summary>
/// Used as event arguments on <see cref="IUnitOfWork.Failed"/> event.
/// </summary>
public class UnitOfWorkFailedEventArgs(IUnitOfWork unitOfWork, System.Exception? exception, bool isRolledback)
    : UnitOfWorkEventArgs(unitOfWork)
{
    /// <summary>
    /// Exception that caused failure.
    /// </summary>
    public System.Exception? Exception { get; private set; } = exception;

    /// <summary>
    /// True, if the unit of work is manually rolled back.
    /// </summary>
    public bool IsRolledback { get; } = isRolledback;
}

