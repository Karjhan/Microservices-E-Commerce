using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Security.Audit;
using Shared.Security.Problems;

namespace Shared.Security.Tests.Problems;

public class AccessProblemHandlerTests
{
    private static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    private static (DefaultHttpContext Context, Mock<IRefusalAuditRecorder> Recorder) CreateContext(bool signedIn)
    {
        var recorder = new Mock<IRefusalAuditRecorder>();
        var services = new ServiceCollection()
            .AddSingleton(recorder.Object)
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/products";

        if (signedIn)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, "Customer") }, "test"));
        }

        return (context, recorder);
    }

    private static async Task<JsonElement> ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    [Fact]
    public async Task Anonymous_caller_gets_401_with_bearer_challenge_and_is_audited()
    {
        var (context, recorder) = CreateContext(signedIn: false);
        var handler = new AccessProblemHandler();

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        (await ReadBody(context)).GetProperty("code").GetString().Should().Be("unauthenticated");
        recorder.Verify(r => r.RecordAsync(context, "unauthenticated", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Signed_in_non_admin_gets_403_and_is_audited()
    {
        var (context, recorder) = CreateContext(signedIn: true);
        var handler = new AccessProblemHandler();

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Forbid());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Headers.WWWAuthenticate.ToString().Should().BeEmpty();
        (await ReadBody(context)).GetProperty("code").GetString().Should().Be("forbidden");
        recorder.Verify(r => r.RecordAsync(context, "forbidden", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Successful_authorization_continues_the_pipeline_without_auditing()
    {
        var (context, recorder) = CreateContext(signedIn: true);
        var handler = new AccessProblemHandler();
        var nextCalled = false;

        await handler.HandleAsync(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            context,
            Policy,
            PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue();
        recorder.Verify(
            r => r.RecordAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Refusal_is_still_returned_when_no_audit_store_is_registered()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        var handler = new AccessProblemHandler();

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
