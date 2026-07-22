using RRBridal.StoreBilling.App.Services;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class StoreMongoOptionsTests
{
    [Fact]
    public void Defaults_use_localhost_and_20s_timeouts()
    {
        var opts = new StoreMongoOptions(null, null, null, null, null);

        Assert.Equal("mongodb://localhost:27017/rr_bridal_store", opts.ConnectionUri);
        Assert.Equal(TimeSpan.FromSeconds(20), opts.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), opts.ServerSelectionTimeout);
        Assert.Null(opts.SocketTimeout);
        Assert.Equal(TimeSpan.FromSeconds(45), opts.HealthCheckInterval);
        Assert.Equal("localhost:27017", opts.HostDisplay);
        Assert.True(opts.RequireReady);
    }

    [Fact]
    public void Parses_env_timeouts_and_host_display()
    {
        var opts = new StoreMongoOptions(
            "mongodb://10.147.20.1:27017/rr_bridal_store01",
            "30",
            "25",
            "60",
            "15",
            "true");

        Assert.Equal(TimeSpan.FromSeconds(30), opts.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(25), opts.ServerSelectionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), opts.SocketTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), opts.HealthCheckInterval);
        Assert.Equal("10.147.20.1:27017", opts.HostDisplay);
        Assert.True(opts.RequireReady);
    }

    [Fact]
    public void Invalid_or_zero_timeouts_fall_back()
    {
        var opts = new StoreMongoOptions(
            "mongodb://parent:27017/db",
            "0",
            "abc",
            "-1",
            null);

        Assert.Equal(TimeSpan.FromSeconds(20), opts.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), opts.ServerSelectionTimeout);
        Assert.Null(opts.SocketTimeout);
        Assert.Equal(TimeSpan.FromSeconds(45), opts.HealthCheckInterval);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("no", false)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData(null, true)]
    public void RequireReady_parses_env_flag(string? raw, bool expected)
    {
        var opts = new StoreMongoOptions(null, null, null, null, null, raw);
        Assert.Equal(expected, opts.RequireReady);
    }
}