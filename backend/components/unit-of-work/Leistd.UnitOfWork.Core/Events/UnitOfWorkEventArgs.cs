namespace Leistd.UnitOfWork.Core.Events;

/// <summary>
/// Unit of work event args
/// </summary>
public class UnitOfWorkEventArgs(Uow.IUnitOfWork unitOfWork) : EventArgs
{
    /// <summary>
    /// Reference to the unit of work related to this event.
    /// </summary>
    public Uow.IUnitOfWork UnitOfWork { get; } = unitOfWork;
}

