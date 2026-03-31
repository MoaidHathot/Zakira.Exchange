using Zakira.Exchange.Core.Configuration;

namespace Zakira.Exchange.Tests.Configuration;

public class AccessModeTests
{
    [Theory]
    [InlineData(AccessMode.Full, true)]
    [InlineData(AccessMode.ReadOnly, false)]
    [InlineData(AccessMode.AppendOnly, true)]
    [InlineData(AccessMode.NoDelete, true)]
    public void CanCreate_ReturnsExpected(AccessMode mode, bool expected)
    {
        Assert.Equal(expected, mode.CanCreate());
    }

    [Theory]
    [InlineData(AccessMode.Full, true)]
    [InlineData(AccessMode.ReadOnly, true)]
    [InlineData(AccessMode.AppendOnly, true)]
    [InlineData(AccessMode.NoDelete, true)]
    public void CanRead_AlwaysReturnsTrue(AccessMode mode, bool expected)
    {
        Assert.Equal(expected, mode.CanRead());
    }

    [Theory]
    [InlineData(AccessMode.Full, true)]
    [InlineData(AccessMode.ReadOnly, false)]
    [InlineData(AccessMode.AppendOnly, false)]
    [InlineData(AccessMode.NoDelete, true)]
    public void CanEdit_ReturnsExpected(AccessMode mode, bool expected)
    {
        Assert.Equal(expected, mode.CanEdit());
    }

    [Theory]
    [InlineData(AccessMode.Full, true)]
    [InlineData(AccessMode.ReadOnly, false)]
    [InlineData(AccessMode.AppendOnly, false)]
    [InlineData(AccessMode.NoDelete, false)]
    public void CanDelete_OnlyFullReturnsTrue(AccessMode mode, bool expected)
    {
        Assert.Equal(expected, mode.CanDelete());
    }
}
