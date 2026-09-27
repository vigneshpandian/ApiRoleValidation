namespace ApiRoleValidation.Services;

public interface IUserCacheService
{
    Task<string?> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetSecurityStampAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default);
    Task InvalidateSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default);
}

