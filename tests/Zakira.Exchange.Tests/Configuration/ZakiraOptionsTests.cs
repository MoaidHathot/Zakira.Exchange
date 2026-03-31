using Zakira.Exchange.Core.Configuration;

namespace Zakira.Exchange.Tests.Configuration;

public class ZakiraOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new ZakiraOptions();

        Assert.Equal("zakira.db", options.DatabasePath);
        Assert.Equal(AccessMode.Full, options.AccessMode);
        Assert.Null(options.ConstCategory);
        Assert.Null(options.ModelPath);
    }

    [Fact]
    public void HasConstCategory_ReturnsFalse_WhenNull()
    {
        var options = new ZakiraOptions { ConstCategory = null };
        Assert.False(options.HasConstCategory);
    }

    [Fact]
    public void HasConstCategory_ReturnsFalse_WhenEmpty()
    {
        var options = new ZakiraOptions { ConstCategory = "" };
        Assert.False(options.HasConstCategory);
    }

    [Fact]
    public void HasConstCategory_ReturnsFalse_WhenWhitespace()
    {
        var options = new ZakiraOptions { ConstCategory = "   " };
        Assert.False(options.HasConstCategory);
    }

    [Fact]
    public void HasConstCategory_ReturnsTrue_WhenSet()
    {
        var options = new ZakiraOptions { ConstCategory = "test-category" };
        Assert.True(options.HasConstCategory);
    }
}
