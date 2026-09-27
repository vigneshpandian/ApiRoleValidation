using System.Security.Claims;
using ApiRoleValidation.DTOs;
using ApiRoleValidation.Models;
using ApiRoleValidation.Repositories;
using ApiRoleValidation.Services;
using Microsoft.AspNetCore.Identity;

namespace ApiRoleValidation.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", async (
            LoginRequest request,
            IUserRepository userRepository,
            ITokenService tokenService,
            IUserCacheService cacheService,
            IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new MessageResponse("Username and password are required."));
            }

            var user = await userRepository.GetByUsernameAsync(request.Username);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var passwordHasher = new PasswordHasher<User>();
            var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user);
            var refreshToken = tokenService.GenerateRefreshToken();
            var refreshDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;
            var refreshExpiry = DateTime.UtcNow.AddDays(refreshDays);

            await userRepository.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);
            await cacheService.SetSecurityStampAsync(user.Id, user.SecurityStamp);

            return Results.Ok(new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                Role: user.Role,
                UserId: user.Id,
                Username: user.Username,
                ExpiresAt: expiresAt
            ));
        })
        .WithName("Login")
        .WithSummary("User login returning access token and refresh token");

        group.MapPost("/refresh-token", async (
            RefreshTokenRequest request,
            IUserRepository userRepository,
            ITokenService tokenService,
            IUserCacheService cacheService,
            IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new MessageResponse("Refresh token is required."));
            }

            User? user = null;

            // If access token is passed, extract user ID first
            if (!string.IsNullOrWhiteSpace(request.AccessToken))
            {
                var principal = tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
                var userIdStr = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    user = await userRepository.GetByIdAsync(userId);
                }
            }

            // Fallback: search all users for matching refresh token
            if (user == null)
            {
                var allUsers = await userRepository.GetAllUsersAsync();
                user = allUsers.FirstOrDefault(u => u.RefreshToken == request.RefreshToken);
            }

            if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return Results.Unauthorized();
            }

            // Single-use rotating refresh token: generate new access token and new refresh token
            var (newAccessToken, expiresAt) = tokenService.GenerateAccessToken(user);
            var newRefreshToken = tokenService.GenerateRefreshToken();
            var refreshDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;
            var refreshExpiry = DateTime.UtcNow.AddDays(refreshDays);

            await userRepository.UpdateRefreshTokenAsync(user.Id, newRefreshToken, refreshExpiry);
            await cacheService.SetSecurityStampAsync(user.Id, user.SecurityStamp);

            return Results.Ok(new AuthResponse(
                AccessToken: newAccessToken,
                RefreshToken: newRefreshToken,
                Role: user.Role,
                UserId: user.Id,
                Username: user.Username,
                ExpiresAt: expiresAt
            ));
        })
        .WithName("RefreshToken")
        .WithSummary("Refresh access token using single-use rotating refresh token");

        group.MapPost("/signout", async (
            ClaimsPrincipal principal,
            IUserRepository userRepository,
            IUserCacheService cacheService) =>
        {
            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.Unauthorized();
            }

            // Rotate security stamp to instantly revoke existing JWTs and clear refresh token
            var newSecurityStamp = Guid.NewGuid().ToString();
            await userRepository.UpdateSecurityStampAsync(userId, newSecurityStamp);
            await userRepository.UpdateRefreshTokenAsync(userId, null, null);
            await cacheService.SetSecurityStampAsync(userId, newSecurityStamp);

            return Results.Ok(new MessageResponse("Signed out successfully. Active access tokens have been revoked."));
        })
        .RequireAuthorization()
        .WithName("SignOut")
        .WithSummary("Sign out current user and immediately revoke all active tokens");

        group.MapPost("/change-role", async (
            ChangeRoleRequest request,
            IUserRepository userRepository,
            IUserCacheService cacheService) =>
        {
            if (request.NewRole != Roles.Admin && request.NewRole != Roles.User)
            {
                return Results.BadRequest(new MessageResponse($"Invalid role: {request.NewRole}. Allowed roles: {Roles.Admin}, {Roles.User}"));
            }

            var targetUser = await userRepository.GetByIdAsync(request.UserId);
            if (targetUser == null)
            {
                return Results.NotFound(new MessageResponse($"User with ID {request.UserId} not found."));
            }

            // Generate new security stamp
            var newSecurityStamp = Guid.NewGuid().ToString();

            // Update DB
            var updated = await userRepository.UpdateRoleAndSecurityStampAsync(request.UserId, request.NewRole, newSecurityStamp);
            if (!updated)
            {
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            // Update HybridCache immediately with the new stamp
            await cacheService.SetSecurityStampAsync(request.UserId, newSecurityStamp);

            return Results.Ok(new MessageResponse(
                $"User '{targetUser.Username}' role successfully changed to '{request.NewRole}'. Prior access tokens have been instantly revoked via SecurityStamp update."
            ));
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("ChangeRole")
        .WithSummary("Admin endpoint to change a user's role and immediately revoke their active tokens");

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value;
            var stamp = principal.FindFirst("security_stamp")?.Value;

            return Results.Ok(new
            {
                UserId = userId,
                Username = username,
                Role = role,
                SecurityStamp = stamp
            });
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Get claims of current authenticated user");

        group.MapGet("/users", async (IUserRepository userRepository) =>
        {
            var users = await userRepository.GetAllUsersAsync();
            var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.Role, u.SecurityStamp));
            return Results.Ok(dtos);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("GetAllUsers")
        .WithSummary("Admin endpoint to list all users and their IDs for testing");

        return group;
    }
}

