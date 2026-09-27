namespace ApiRoleValidation.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.User;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

