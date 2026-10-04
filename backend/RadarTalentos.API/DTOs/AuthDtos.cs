namespace RadarTalentos.API.DTOs;

public record LoginRequest(string Email, string Password);
public record UserDto(Guid Id, string Name, string Email, string Role, bool IsActive);
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public record CreateUserRequest(string Name, string Email, string Password, string Role);
public record UpdateUserRequest(string Name, string Role, bool IsActive, string? Password);
