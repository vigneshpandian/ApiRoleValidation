using System.Security.Claims;
using ApiRoleValidation.Models;

namespace ApiRoleValidation.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

