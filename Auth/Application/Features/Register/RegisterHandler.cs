using Contracts.Events;
using Domain.Abstractions.Authentication;
using Domain.Abstractions.Messaging;
using Domain.Abstractions.Persistence;
using Domain.Entities;

namespace Application.Features.Register;

public class RegisterHandler
{
    private readonly IUserRepository _repo;
    private readonly IEventPublisher _publisher;

    public RegisterHandler(IUserRepository repo, IEventPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(RegisterCommand command)
    {
        var existing = await _repo.GetByEmailAsync(command.Email);
        if (existing != null)
            throw new Exception("User already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            NormalizedEmail = command.Email.Trim().ToLowerInvariant(),  
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password)
        };

        await _repo.AddAsync(user);
        
        await _publisher.PublishAsync(
            new UserRegisteredEvent(user.Id, user.Email, user.Role),
            "user.registered"
        );

        return user.Id;
    }
}