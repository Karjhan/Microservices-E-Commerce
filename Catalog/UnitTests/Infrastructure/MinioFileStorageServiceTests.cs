using FluentAssertions;
using Infrastructure.Configuration;
using Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using Moq;

namespace UnitTests.Infrastructure;

public class MinioFileStorageServiceTests
{
    private readonly Mock<IMinioClient> _minioMock = new();

    private MinioFileStorageService CreateService()
    {
        var options = Options.Create(new MinioOptions
        {
            Bucket = "test-bucket",
            Endpoint = "http://localhost:9000"
        });

        return new MinioFileStorageService(_minioMock.Object, options);
    }

    [Fact]
    public async Task UploadAsync_ShouldUploadFileAndReturnUrl()
    {
        // Arrange
        var service = CreateService();

        _minioMock.Setup(x => x.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _minioMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ReturnsAsync(new PutObjectResponse(
                System.Net.HttpStatusCode.OK,
                "",
                new Dictionary<string, string>(),
                0,
                "obj-key"
            ));

        // Act
        var result = await service.UploadAsync("file.txt", "obj-key", "text/plain");

        // Assert
        result.Should().Be("http://localhost:9000/test-bucket/obj-key");
    }

    [Fact]
    public async Task UploadAsync_ShouldCreateBucketIfNotExists()
    {
        // Arrange
        var service = CreateService();

        _minioMock.Setup(x => x.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _minioMock.Setup(x => x.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _minioMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ReturnsAsync(new PutObjectResponse(
                System.Net.HttpStatusCode.OK,
                "",
                new Dictionary<string, string>(),
                0,
                "obj-key"
            ));

        // Act
        await service.UploadAsync("file.txt", "obj-key", "text/plain");

        // Assert
        _minioMock.Verify(x =>
            x.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveObject()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.DeleteAsync("obj-key");

        // Assert
        _minioMock.Verify(x =>
            x.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}