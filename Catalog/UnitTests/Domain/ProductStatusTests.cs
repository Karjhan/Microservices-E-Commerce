using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using UnitTests.Helpers;

namespace UnitTests.DomainModel;

public class ProductStatusTests
{
    [Fact]
    public void A_New_Product_Is_A_Draft()
    {
        TestDataBuilder.CreateProduct().Status.Should().Be(ProductStatus.Draft);
    }

    [Theory]
    [InlineData(ProductStatus.Draft, ProductStatus.Active)]
    [InlineData(ProductStatus.Active, ProductStatus.Draft)]
    [InlineData(ProductStatus.Active, ProductStatus.Inactive)]
    [InlineData(ProductStatus.Inactive, ProductStatus.Active)]
    [InlineData(ProductStatus.Active, ProductStatus.OutOfStock)]
    [InlineData(ProductStatus.OutOfStock, ProductStatus.Active)]
    [InlineData(ProductStatus.Draft, ProductStatus.Discontinued)]
    [InlineData(ProductStatus.Active, ProductStatus.Discontinued)]
    [InlineData(ProductStatus.Inactive, ProductStatus.Discontinued)]
    [InlineData(ProductStatus.OutOfStock, ProductStatus.Discontinued)]
    public void Allowed_Transitions_Succeed(ProductStatus from, ProductStatus to)
    {
        ProductStatusRules.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(ProductStatus.Draft, ProductStatus.Inactive)]
    [InlineData(ProductStatus.Draft, ProductStatus.OutOfStock)]
    [InlineData(ProductStatus.Inactive, ProductStatus.Draft)]
    [InlineData(ProductStatus.Discontinued, ProductStatus.Active)]
    [InlineData(ProductStatus.Discontinued, ProductStatus.Draft)]
    public void Other_Transitions_Are_Refused(ProductStatus from, ProductStatus to)
    {
        ProductStatusRules.CanTransition(from, to).Should().BeFalse();
    }

    [Fact]
    public void ChangeStatus_Moves_The_Product_And_Reports_The_Change()
    {
        var product = TestDataBuilder.CreateProduct();

        product.ChangeStatus(ProductStatus.Active).Should().BeTrue();

        product.Status.Should().Be(ProductStatus.Active);
        product.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeStatus_To_The_Same_Status_Is_A_No_Op()
    {
        var product = TestDataBuilder.CreateProduct();

        product.ChangeStatus(ProductStatus.Draft).Should().BeFalse();
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_Refuses_A_Forbidden_Transition_With_A_Business_Rule_Error()
    {
        var product = TestDataBuilder.CreateProduct();
        product.ChangeStatus(ProductStatus.Discontinued);

        var act = () => product.ChangeStatus(ProductStatus.Active);

        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("invalid_status_transition");
        product.Status.Should().Be(ProductStatus.Discontinued);
    }
}
