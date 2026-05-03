using FluentAssertions;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace UnitTests.Infrastructure;

public class RedisCacheServiceTests
{
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<IConnectionMultiplexer> _muxMock = new();

    private RedisCacheService CreateService()
    {
        _muxMock
            .Setup(x => x.GetDatabase())
            .Returns(_dbMock.Object);

        var options = Options.Create(new RedisOptions
        {
            DefaultTtlSeconds = 60
        });

        return new RedisCacheService(_muxMock.Object, options);
    }

    [Fact]
    public async Task SetAsync_ShouldSerializeValueCorrectly()
    {
        // Arrange
        var service = CreateService();

        RedisKey capturedKey = default;

        _dbMock
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>(
                (key, value, expiry, _, _) =>
                {
                    capturedKey = key;
                })
            .ReturnsAsync(true);

        var input = new { Name = "Test" };

        // Act
        await service.SetAsync("(null)", input);

        // Assert
        capturedKey.ToString().Should().Be("(null)");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDeserializedObject()
    {
        // Arrange
        var service = CreateService();
        var json = "{\"Name\":\"Test\"}";

        _dbMock.Setup(x => x.StringGetAsync("key", CommandFlags.None))
            .ReturnsAsync(json);

        // Act
        var result = await service.GetAsync<TestObj>("key");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteKey()
    {
        // Arrange
        var service = CreateService();

        _dbMock.Setup(x => x.KeyDeleteAsync("key", CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        await service.RemoveAsync("key");

        // Assert
        _dbMock.Verify(x => x.KeyDeleteAsync("key", CommandFlags.None), Times.Once);
    }

    private class TestObj
    {
        public string Name { get; set; } = default!;
    }
}