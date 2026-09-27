using ApiRoleValidation.Models;

namespace ApiRoleValidation.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<IReadOnlyList<User>> GetAllUsersAsync();
    Task<bool> UpdateRoleAndSecurityStampAsync(Guid userId, string newRole, string newSecurityStamp);
    Task<bool> UpdateSecurityStampAsync(Guid userId, string newSecurityStamp);
    Task<bool> UpdateRefreshTokenAsync(Guid userId, string? refreshToken, DateTime? expiryTime);
}

