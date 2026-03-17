using System.Data;

namespace Leistd.UnitOfWork.Core.Options;

/// <inheritdoc />
public class UnitOfWorkOptions : IUnitOfWorkOptions
{
    /// <inheritdoc />
    public bool IsTransactional { get; set; }

    /// <inheritdoc />
    public IsolationLevel? IsolationLevel { get; set; }

    /// <inheritdoc />
    public TimeSpan? Timeout { get; set; }

    /// <inheritdoc />
    public UnitOfWorkOptions()
    {
    }

    /// <inheritdoc />
    public UnitOfWorkOptions Clone()
    {
        return new UnitOfWorkOptions
        {
            IsTransactional = IsTransactional,
            IsolationLevel = IsolationLevel,
            Timeout = Timeout
        };
    }
}
