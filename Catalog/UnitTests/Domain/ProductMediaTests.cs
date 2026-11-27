using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using UnitTests.Helpers;

namespace UnitTests.DomainModel;

public class ProductMediaTests
{
    private static ProductImage Image(Product product, bool primary = false) =>
        new(Guid.NewGuid(), product.Id, "key", "url", "image/png", 10, 0, primary);

    [Fact]
    public void First_Image_Is_Primary_And_First_In_Order()
    {
        var product = TestDataBuilder.CreateProduct();

        product.AddImage(Image(product));

        product.Images.Single().IsPrimary.Should().BeTrue();
        product.Images.Single().SortOrder.Should().Be(0);
    }

    [Fact]
    public void Images_Are_Appended_In_Order()
    {
        var product = TestDataBuilder.CreateProduct();

        product.AddImage(Image(product));
        product.AddImage(Image(product));
        product.AddImage(Image(product));

        product.Images.Select(x => x.SortOrder).Should().Equal(0, 1, 2);
        product.Images.Count(x => x.IsPrimary).Should().Be(1);
    }

    [Fact]
    public void SetPrimaryImage_Leaves_Exactly_One_Primary()
    {
        var product = TestDataBuilder.CreateProduct();
        product.AddImage(Image(product));
        var second = Image(product);
        product.AddImage(second);

        product.SetPrimaryImage(second.Id);

        product.Images.Single(x => x.IsPrimary).Id.Should().Be(second.Id);
    }

    [Fact]
    public void SetPrimaryImage_Throws_For_An_Unknown_Image()
    {
        var product = TestDataBuilder.CreateProduct();
        product.AddImage(Image(product));

        var act = () => product.SetPrimaryImage(Guid.NewGuid());

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ReorderImages_Applies_The_Given_Order()
    {
        var product = TestDataBuilder.CreateProduct();
        var a = Image(product);
        var b = Image(product);
        var c = Image(product);
        product.AddImage(a);
        product.AddImage(b);
        product.AddImage(c);

        product.ReorderImages([c.Id, a.Id, b.Id]);

        product.Images.OrderBy(x => x.SortOrder).Select(x => x.Id).Should().Equal(c.Id, a.Id, b.Id);
    }

    [Fact]
    public void ReorderImages_Requires_Every_Image_Exactly_Once()
    {
        var product = TestDataBuilder.CreateProduct();
        var a = Image(product);
        var b = Image(product);
        product.AddImage(a);
        product.AddImage(b);

        var missing = () => product.ReorderImages([a.Id]);
        var duplicated = () => product.ReorderImages([a.Id, a.Id]);
        var foreign = () => product.ReorderImages([a.Id, Guid.NewGuid()]);

        missing.Should().Throw<ValidationFailedException>();
        duplicated.Should().Throw<ValidationFailedException>();
        foreign.Should().Throw<ValidationFailedException>();
    }

    [Fact]
    public void Removing_The_Primary_Then_Promoting_Picks_The_Lowest_Sort_Order()
    {
        var product = TestDataBuilder.CreateProduct();
        var a = Image(product);
        var b = Image(product);
        var c = Image(product);
        product.AddImage(a);
        product.AddImage(b);
        product.AddImage(c);

        product.RemoveImage(a.Id);
        var promoted = product.PromoteLowestImage();

        promoted!.Id.Should().Be(b.Id);
        product.Images.Single(x => x.IsPrimary).Id.Should().Be(b.Id);
    }

    [Fact]
    public void Promote_Does_Nothing_When_A_Primary_Exists_Or_No_Images_Remain()
    {
        var product = TestDataBuilder.CreateProduct();
        product.PromoteLowestImage().Should().BeNull();

        product.AddImage(Image(product));
        product.PromoteLowestImage().Should().BeNull();
    }

    [Theory]
    [InlineData("part.glb", "glb", true)]
    [InlineData("scene.GLTF", "gltf", true)]
    [InlineData("benchy.stl", "stl", true)]
    [InlineData("print.3mf", "3mf", false)]
    [InlineData("shape.obj", "obj", false)]
    public void Model_Format_And_Preview_Follow_The_Extension(string filename, string format, bool previewable)
    {
        var model = new ProductModel(Guid.NewGuid(), Guid.NewGuid(), ProductModel.FormatOf(filename)!, filename, 10);

        model.Format.Should().Be(format);
        model.IsPreviewable.Should().Be(previewable);
        model.ObjectKey.Should().EndWith($".{format}").And.Contain("/model/");
    }

    [Fact]
    public void Model_Filename_Is_Sanitized_For_Display()
    {
        ProductModel.SanitizeFilename("..\\..\\evil\u0000.glb").Should().Be("evil.glb");
        ProductModel.SanitizeFilename("   ").Should().Be("model");
    }
}
