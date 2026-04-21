using Domain.Entities;

namespace Domain.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task AddRefreshTokenAsync(RefreshToken token);
    Task AddAsync(User user);
}