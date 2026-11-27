using FluentAssertions;
using Infrastructure.Storage;
using UnitTests.Helpers;

namespace UnitTests.Infrastructure;

public class FileSignatureValidatorTests
{
    [Fact]
    public void Detects_Jpeg()
    {
        FileSignatureValidator.DetectImageContentType(TestImages.Jpeg()).Should().Be("image/jpeg");
    }

    [Fact]
    public void Detects_Png()
    {
        FileSignatureValidator.DetectImageContentType(TestImages.Png()).Should().Be("image/png");
    }

    [Fact]
    public void Detects_WebP()
    {
        FileSignatureValidator.DetectImageContentType(TestImages.WebP()).Should().Be("image/webp");
    }

    [Fact]
    public void Rejects_An_Executable_Whatever_Its_Name()
    {
        FileSignatureValidator.DetectImageContentType(TestImages.Executable()).Should().BeNull();
    }

    [Fact]
    public void Rejects_A_Riff_File_That_Is_Not_WebP()
    {
        FileSignatureValidator.DetectImageContentType(TestImages.Wav()).Should().BeNull();
    }

    [Fact]
    public void Rejects_Empty_And_Truncated_Content()
    {
        FileSignatureValidator.DetectImageContentType([]).Should().BeNull();
        FileSignatureValidator.DetectImageContentType(TestImages.Png()[..4]).Should().BeNull();
        FileSignatureValidator.DetectImageContentType(TestImages.WebP()[..8]).Should().BeNull();
    }

    [Fact]
    public void Rejects_Text_Pretending_To_Be_An_Image()
    {
        FileSignatureValidator.DetectImageContentType("fake image content"u8).Should().BeNull();
    }

    [Fact]
    public async Task Async_Detection_Rewinds_The_Stream()
    {
        using var stream = new MemoryStream(TestImages.Png());

        var type = await FileSignatureValidator.DetectImageContentTypeAsync(stream);

        type.Should().Be("image/png");
        stream.Position.Should().Be(0);
    }

    [Fact]
    public async Task Async_Detection_Handles_A_Stream_Shorter_Than_The_Header()
    {
        using var stream = new MemoryStream([0xFF, 0xD8]);

        (await FileSignatureValidator.DetectImageContentTypeAsync(stream)).Should().BeNull();
    }
}
