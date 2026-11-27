using Shared.Security.Authorization;

namespace UnitTests.Helpers;

/// <summary>Caller stand-in for handler tests.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? Id { get; init; }
    public string? Email { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsAuthenticated { get; init; }
    public bool IsAdmin => IsAuthenticated && Role == Roles.Admin;
    public string? CorrelationId => null;

    public static FakeCurrentUser Admin() =>
        new() { Id = Guid.NewGuid(), Role = Roles.Admin, IsAuthenticated = true };

    public static FakeCurrentUser Customer(Guid id) =>
        new() { Id = id, Role = Roles.Customer, IsAuthenticated = true };

    public static FakeCurrentUser Anonymous() => new();
}