using ApiRoleValidation.Data;
using ApiRoleValidation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiRoleValidation.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync()
    {
        return await _context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<bool> UpdateRoleAndSecurityStampAsync(Guid userId, string newRole, string newSecurityStamp)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        user.Role = newRole;
        user.SecurityStamp = newSecurityStamp;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateSecurityStampAsync(Guid userId, string newSecurityStamp)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        user.SecurityStamp = newSecurityStamp;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateRefreshTokenAsync(Guid userId, string? refreshToken, DateTime? expiryTime)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = expiryTime;
        await _context.SaveChangesAsync();
        return true;
    }
}

