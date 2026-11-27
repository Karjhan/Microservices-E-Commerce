using FluentAssertions;
using Shared.Security.Authorization;

namespace Shared.Security.Tests.Authorization;

public class RoleNormalizationTests
{
    [Fact]
    public void Admin_stays_admin()
    {
        Roles.NormalizeRole("Admin").Should().Be(Roles.Admin);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Support")]
    [InlineData("Customer")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData(" Admin")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_becomes_customer(string? stored)
    {
        Roles.NormalizeRole(stored).Should().Be(Roles.Customer);
    }
}
