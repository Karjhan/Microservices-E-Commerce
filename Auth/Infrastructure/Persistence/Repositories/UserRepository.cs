using Domain.Abstractions.Authentication;
using Domain.Abstractions.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();

        return await _db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
    }
    
    public async Task AddRefreshTokenAsync(RefreshToken token)
    {
        await _db.RefreshTokens.AddAsync(token);
        await _db.SaveChangesAsync();
    }

    public async Task AddAsync(User user)
    {
        user.Email = user.Email.ToLower();
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
    }
}