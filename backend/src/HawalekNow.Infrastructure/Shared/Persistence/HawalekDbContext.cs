using HawalekNow.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HawalekNow.Infrastructure.Shared.Persistence;

public sealed class HawalekDbContext(DbContextOptions<HawalekDbContext> options) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configurations remain owned by their modules; Shared has no module implementation dependency.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HawalekDbContext).Assembly);
    }

    public async Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
        new Transaction(await Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        // Disposing an uncommitted transaction rolls it back, including SaveChanges performed by Identity stores.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
