using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Payments;

namespace UnitTests.Http;

public class RecipientEmailResolverTests
{
    [Fact]
    public void Should_Prefer_Request_Value()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Email"] = "from-jwt@example.com";

        var result = RecipientEmailResolver.Resolve(context, "  from-body@example.com  ");

        result.Should().Be("from-body@example.com");
    }

    [Fact]
    public void Should_Fallback_To_Header_When_Request_Empty()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Email"] = "from-jwt@example.com";

        var result = RecipientEmailResolver.Resolve(context, null);

        result.Should().Be("from-jwt@example.com");
    }
}