namespace Contracts.Events;

public record UserLoggedInEvent(
    Guid UserId,
    string Email
);