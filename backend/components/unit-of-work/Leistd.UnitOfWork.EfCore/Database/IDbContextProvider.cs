using Microsoft.EntityFrameworkCore;

namespace Leistd.UnitOfWork.EfCore.Database;

/// <summary>
/// DbContext provider interface
/// </summary>
public interface IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Get DbContext asynchronously
    /// </summary>
    Task<TDbContext> GetDbContextAsync(CancellationToken cancellationToken = default);
}
