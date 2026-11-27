using Application.Abstractions.Storage;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Infrastructure.BackgroundJobs;
using Moq;

namespace UnitTests.Infrastructure;

public class OrphanedObjectSweepJobTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Old = Now.AddHours(-3);
    private static readonly DateTime Fresh = Now.AddMinutes(-10);

    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();

    private void Arrange(string[] imageRefs, string[] modelRefs, StoredObject[] images, StoredObject[] models)
    {
        _repo.Setup(x => x.GetReferencedObjectKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((imageRefs.ToHashSet(), modelRefs.ToHashSet()));
        _storage.Setup(x => x.ListObjectsAsync(StorageBucket.Images, It.IsAny<CancellationToken>())).ReturnsAsync(images);
        _storage.Setup(x => x.ListObjectsAsync(StorageBucket.Models, It.IsAny<CancellationToken>())).ReturnsAsync(models);
    }

    [Fact]
    public async Task Removes_Old_Unreferenced_Objects_In_Both_Buckets()
    {
        Arrange([], [],
            [new("products/a/images/1", Old)],
            [new("products/a/model/1.glb", Old)]);

        var removed = await OrphanedObjectSweepJob.SweepAsync(_repo.Object, _storage.Object, Now, CancellationToken.None);

        removed.Should().Be(2);
        _storage.Verify(x => x.DeleteAsync(StorageBucket.Images, "products/a/images/1", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(x => x.DeleteAsync(StorageBucket.Models, "products/a/model/1.glb", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Keeps_Referenced_Objects()
    {
        Arrange(["products/a/images/1"], ["products/a/model/1.glb"],
            [new("products/a/images/1", Old)],
            [new("products/a/model/1.glb", Old)]);

        var removed = await OrphanedObjectSweepJob.SweepAsync(_repo.Object, _storage.Object, Now, CancellationToken.None);

        removed.Should().Be(0);
        _storage.Verify(x => x.DeleteAsync(It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Keeps_Objects_Younger_Than_The_Grace_Period_Because_An_Upload_May_Be_In_Flight()
    {
        Arrange([], [], [new("products/a/images/1", Fresh)], [new("products/a/model/1.glb", Fresh)]);

        var removed = await OrphanedObjectSweepJob.SweepAsync(_repo.Object, _storage.Object, Now, CancellationToken.None);

        removed.Should().Be(0);
    }

    [Fact]
    public async Task Leaves_Image_Bucket_Objects_Outside_The_Product_Prefix_Alone()
    {
        Arrange([], [], [new("logos/brand.png", Old)], []);

        var removed = await OrphanedObjectSweepJob.SweepAsync(_repo.Object, _storage.Object, Now, CancellationToken.None);

        removed.Should().Be(0);
    }
}
