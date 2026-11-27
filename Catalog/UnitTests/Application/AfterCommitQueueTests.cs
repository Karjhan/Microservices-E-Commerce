using Application.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Application;

public class AfterCommitQueueTests
{
    private readonly AfterCommitQueue _queue = new(NullLogger<AfterCommitQueue>.Instance);

    [Fact]
    public async Task Runs_Registered_Work_In_Order_Exactly_Once()
    {
        var order = new List<int>();
        _queue.Register(_ => { order.Add(1); return Task.CompletedTask; });
        _queue.Register(_ => { order.Add(2); return Task.CompletedTask; });

        await _queue.RunAsync(CancellationToken.None);
        await _queue.RunAsync(CancellationToken.None);

        order.Should().Equal(1, 2);
    }

    [Fact]
    public async Task A_Failing_Step_Does_Not_Stop_The_Others()
    {
        var ran = false;
        _queue.Register(_ => throw new InvalidOperationException("boom"));
        _queue.Register(_ => { ran = true; return Task.CompletedTask; });

        await _queue.RunAsync(CancellationToken.None);

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task Cleared_Work_Never_Runs()
    {
        var ran = false;
        _queue.Register(_ => { ran = true; return Task.CompletedTask; });

        _queue.Clear();
        await _queue.RunAsync(CancellationToken.None);

        ran.Should().BeFalse();
    }
}
