using ApiRoleValidation.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace ApiRoleValidation.Services;

public class UserCacheService : IUserCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserCacheService> _logger;

    public UserCacheService(
        HybridCache hybridCache,
        IUserRepository userRepository,
        ILogger<UserCacheService> logger)
    {
        _hybridCache = hybridCache;
        _userRepository = userRepository;
        _logger = logger;
    }

    private static string GetCacheKey(Guid userId) => $"user_sec_{userId}";

    public async Task<string?> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId);

        var stamp = await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async cancelToken =>
            {
                _logger.LogInformation("Cache miss for security stamp of user {UserId}. Querying database...", userId);
                var user = await _userRepository.GetByIdAsync(userId);
                return user?.SecurityStamp ?? string.Empty;
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(60),
                LocalCacheExpiration = TimeSpan.FromMinutes(60)
            },
            cancellationToken: cancellationToken);

        return string.IsNullOrEmpty(stamp) ? null : stamp;
    }

    public async Task SetSecurityStampAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId);
        _logger.LogInformation("Setting security stamp in HybridCache for user {UserId}...", userId);

        await _hybridCache.SetAsync(
            cacheKey,
            securityStamp,
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(60),
                LocalCacheExpiration = TimeSpan.FromMinutes(60)
            },
            cancellationToken: cancellationToken);
    }

    public async Task InvalidateSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId);
        _logger.LogInformation("Evicting security stamp from HybridCache for user {UserId}...", userId);
        await _hybridCache.RemoveAsync(cacheKey, cancellationToken);
    }
}

