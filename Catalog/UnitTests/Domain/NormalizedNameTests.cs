using Domain.Commons;
using FluentAssertions;

namespace UnitTests.DomainModel;

public class NormalizedNameTests
{
    [Theory]
    [InlineData("  Prusa  ", "prusa")]
    [InlineData("PRUSA", "prusa")]
    [InlineData("Silk   PLA", "silk pla")]
    [InlineData("\tSilk \n PLA  ", "silk pla")]
    [InlineData("Ünïcode Name", "ünïcode name")]
    public void Of_Should_Trim_Lowercase_And_Collapse_Whitespace(string input, string expected)
    {
        NormalizedName.Of(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_Should_Return_Empty_For_Blank_Input(string? input)
    {
        NormalizedName.Of(input).Should().BeEmpty();
    }

    [Fact]
    public void Clean_Should_Keep_The_Original_Casing()
    {
        NormalizedName.Clean("  Silk   PLA ").Should().Be("Silk PLA");
    }

    [Fact]
    public void Names_Differing_Only_In_Case_Or_Spacing_Should_Be_Equal()
    {
        NormalizedName.Of("Bambu Lab").Should().Be(NormalizedName.Of("  bambu   LAB"));
    }
}
