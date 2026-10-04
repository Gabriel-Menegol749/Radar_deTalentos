using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;

namespace RadarTalentos.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    public static UserDto ToDto(User u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(req.Password ?? "", user.PasswordHash))
            throw new AppException(401, "INVALID_CREDENTIALS", "E-mail ou senha inválidos.");
        var (token, expiresAt) = tokens.Create(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user == null || !user.IsActive) throw new AppException(401, "INACTIVE", "Sessão inválida.");
        return ToDto(user);
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db) : ControllerBase
{
    private static void Validate(string? name, string role, string? password, bool passwordRequired)
    {
        if (string.IsNullOrWhiteSpace(name)) throw AppException.BadRequest("Informe o nome.");
        if (!Roles.All.Contains(role)) throw AppException.BadRequest("Perfil inválido.");
        if ((passwordRequired || !string.IsNullOrEmpty(password)) && (password ?? "").Length < 8)
            throw AppException.BadRequest("A senha deve ter pelo menos 8 caracteres.");
    }

    [HttpGet]
    public async Task<List<UserDto>> List() =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync()).Select(AuthController.ToDto).ToList();

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req)
    {
        Validate(req.Name, req.Role, req.Password, passwordRequired: true);
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$"))
            throw AppException.BadRequest("Informe um e-mail válido (ex.: nome@empresa.com).");
        if (await db.Users.AnyAsync(u => u.Email == email)) throw AppException.Conflict("Já existe um usuário com este e-mail.");
        var user = new User { Name = req.Name.Trim(), Email = email, Role = req.Role, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return AuthController.ToDto(user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest req)
    {
        Validate(req.Name, req.Role, req.Password, passwordRequired: false);
        var user = await db.Users.FindAsync(id) ?? throw AppException.NotFound("Usuário");
        if (id == User.UserId() && (!req.IsActive || req.Role != Roles.Admin))
            throw AppException.BadRequest("Você não pode remover seu próprio acesso de Admin.");
        user.Name = req.Name.Trim();
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        if (!string.IsNullOrEmpty(req.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        await db.SaveChangesAsync();
        return AuthController.ToDto(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (id == User.UserId()) throw AppException.BadRequest("Você não pode inativar a si mesmo.");
        var user = await db.Users.FindAsync(id) ?? throw AppException.NotFound("Usuário");
        user.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
