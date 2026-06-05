using Application.Abstractions.Inventory;
using Application.Abstractions.Persistence;

namespace UnitTests.Helpers;

public sealed class FakeTransactionScope : ITransactionScope
{
    public bool CommittedCalled { get; private set; }
    public bool RollbackCalled { get; private set; }
    public bool DisposedCalled { get; private set; }

    public Task CommitAsync(CancellationToken ct)
    {
        CommittedCalled = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct)
    {
        RollbackCalled = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposedCalled = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeReservationPolicy : IReservationPolicy
{
    public FakeReservationPolicy(TimeSpan? timeout = null)
    {
        ReservationTimeout = timeout ?? TimeSpan.FromMinutes(30);
    }

    public TimeSpan ReservationTimeout { get; }
}
