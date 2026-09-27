namespace ApiRoleValidation.DTOs;

public record LoginRequest(string Username, string Password);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string Role,
    Guid UserId,
    string Username,
    DateTime ExpiresAt);

public record RefreshTokenRequest(string RefreshToken, string? AccessToken = null);

public record ChangeRoleRequest(Guid UserId, string NewRole);

public record UserDto(Guid Id, string Username, string Role, string SecurityStamp);

public record MessageResponse(string Message);

