using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CVWebsite.Data;
using CVWebsite.Models;
using CVWebsite.Services;

namespace CVWebsite.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService   _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db  = db;
        _jwt = jwt;
    }

    /// <summary>POST /api/auth/register — { email, password } → { token, user }</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        if (req.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });

        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "Email already registered." });

        var user = new User
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Plan         = "free"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.Generate(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.Plan } });
    }

    /// <summary>POST /api/auth/login — { email, password } → { token, user }</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        var email = req.Email.Trim().ToLower();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var token = _jwt.Generate(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.Plan } });
    }

    /// <summary>GET /api/auth/me — returns current user from JWT</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;

        if (sub == null || !int.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new { user.Id, user.Email, user.Plan });
    }
}

public record AuthRequest(string Email, string Password);
