using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Customers;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class CustomerCodeGeneratorTests
{
    [Fact]
    public async Task NextAsync_OnlineMode_LeavesCodeForCentralWithoutContactingLocalMongo()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1/test");
        settings.ConnectTimeout = TimeSpan.FromMilliseconds(50);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(50);
        var unavailableLocalDb = new MongoClient(settings).GetDatabase("test");
        var generator = new CustomerCodeGenerator(unavailableLocalDb, () => true);

        var code = await generator.NextAsync();

        Assert.Equal("", code);
    }
}
