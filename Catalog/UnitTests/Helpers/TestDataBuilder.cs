using Domain.Entities;

namespace UnitTests.Helpers;

public static class TestDataBuilder
{
    public static Product CreateProduct()
    {
        return new Product(
            "Test Product",
            "Short desc",
            "Long desc",
            100,
            Guid.NewGuid()
        );
    }
}