using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>DispatchGuard 兜底包装（RFC §10 不闪退，行 24 V3 发现面）单测。</summary>
public sealed class DispatchGuardTests
{
    [Fact]
    public void Wrap_正常回调_直通且兜底不触发()
    {
        var calls = 0;
        var fallback = 0;
        var wrapped = DispatchGuard.Wrap(() => calls++, _ => fallback++);

        wrapped();

        Assert.Equal(1, calls);
        Assert.Equal(0, fallback);
    }

    [Fact]
    public void Wrap_回调抛异常_转交兜底且不向外传播()
    {
        Exception? captured = null;
        var wrapped = DispatchGuard.Wrap(
            () => throw new InvalidOperationException("boom"),
            ex => captured = ex);

        var propagated = Record.Exception(() => wrapped());

        Assert.Null(propagated);
        Assert.NotNull(captured);
        Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("boom", captured.Message);
    }

    [Fact]
    public void Wrap_兜底自身抛异常_亦吞且不向外传播()
    {
        var wrapped = DispatchGuard.Wrap(
            () => throw new InvalidOperationException("boom"),
            _ => throw new IOException("fallback failed"));

        var propagated = Record.Exception(() => wrapped());

        Assert.Null(propagated);
    }

    [Fact]
    public void Wrap_多次调用_每次独立兜底()
    {
        var calls = 0;
        var fallbacks = 0;
        var wrapped = DispatchGuard.Wrap(
            () =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("first");
                }
            },
            _ => fallbacks++);

        wrapped();
        wrapped();

        Assert.Equal(2, calls);
        Assert.Equal(1, fallbacks);
    }
}
