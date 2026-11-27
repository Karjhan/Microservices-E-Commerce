using Application.Common;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Moq;

namespace UnitTests.Application;

public class ProductRulesTests
{
    private readonly Mock<IProductRepository> _repo = new();

    private Task Validate(string? name = "Valid name", decimal price = 10.50m, string? currency = "EUR", Guid? id = null) =>
        ProductRules.ValidateAsync(_repo.Object, id, name, price, currency, CancellationToken.None);

    private static async Task<IReadOnlyDictionary<string, string[]>> ErrorsOf(Task task)
    {
        var act = () => task;
        return (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors;
    }

    [Fact]
    public async Task Accepts_A_Valid_Product()
    {
        await Validate();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task Rejects_A_Name_Shorter_Than_Two_Characters(string? name)
    {
        (await ErrorsOf(Validate(name))).Should().ContainKey("name");
    }

    [Fact]
    public async Task Accepts_Names_Of_Exactly_Two_And_One_Hundred_Twenty_Characters()
    {
        await Validate(new string('a', 2));
        await Validate(new string('a', 120));
    }

    [Fact]
    public async Task Rejects_A_Name_Longer_Than_One_Hundred_Twenty_Characters()
    {
        (await ErrorsOf(Validate(new string('a', 121)))).Should().ContainKey("name");
    }

    [Fact]
    public async Task Rejects_A_Duplicate_Name_And_Ignores_The_Product_Being_Edited()
    {
        var editing = Guid.NewGuid();
        _repo.Setup(x => x.NameExistsAsync("Taken", editing, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var errors = await ErrorsOf(Validate("Taken", id: editing));

        errors["name"].Single().Should().Contain("already exists");
        _repo.Verify(x => x.NameExistsAsync("Taken", editing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Checks_The_Trimmed_Name_For_Duplicates()
    {
        await Validate("  Trimmed  ");

        _repo.Verify(x => x.NameExistsAsync("Trimmed", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rejects_A_Negative_Price()
    {
        (await ErrorsOf(Validate(price: -0.01m))).Should().ContainKey("price");
    }

    [Fact]
    public async Task Accepts_A_Zero_Price()
    {
        await Validate(price: 0m);
    }

    [Fact]
    public async Task Rejects_More_Than_Two_Decimal_Places()
    {
        (await ErrorsOf(Validate(price: 1.234m))).Should().ContainKey("price");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("RON")]
    public async Task Rejects_A_Currency_Other_Than_Eur(string currency)
    {
        (await ErrorsOf(Validate(currency: currency))).Should().ContainKey("currency");
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Accepts_Eur_In_Any_Case_Or_No_Currency(string? currency)
    {
        await Validate(currency: currency);
    }

    [Fact]
    public async Task Reports_Every_Problem_At_Once()
    {
        var errors = await ErrorsOf(Validate(name: "x", price: -1m, currency: "USD"));

        errors.Keys.Should().BeEquivalentTo("name", "price", "currency");
    }

    [Theory]
    [InlineData(null, "EUR")]
    [InlineData(" eur ", "EUR")]
    public void Normalizes_The_Currency(string? input, string expected)
    {
        ProductRules.NormalizeCurrency(input).Should().Be(expected);
    }
}
