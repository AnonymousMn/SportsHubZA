using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

/// <summary>
/// Handles administrator sign-in. Passwords are never stored: only a salted hash made by the
/// ASP.NET Core PasswordHasher. The first administrator is created at startup from configuration
/// (user-secrets or environment variables), so no password is ever written into the source code.
/// </summary>
public class AdminAccountService
{
    private const int MinimumPasswordLength = 10;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminAccountService> _logger;
    private readonly PasswordHasher<AdminUser> _hasher = new();
    private readonly string _dummyHash;

    public AdminAccountService(
        IDbContextFactory<AppDbContext> factory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AdminAccountService> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;

        // Used to spend the same time checking an unknown e-mail as a known one.
        _dummyHash = _hasher.HashPassword(new AdminUser(), Guid.NewGuid().ToString());
    }

    /// <summary>Returns the administrator if the e-mail and password are correct, otherwise null.</summary>
    public async Task<AdminUser?> ValidateAsync(string? email, string? password)
    {
        var normalisedEmail = InputNormaliser.Email(email);

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Email == normalisedEmail);

        if (user is null || string.IsNullOrEmpty(password))
        {
            _hasher.VerifyHashedPassword(new AdminUser(), _dummyHash, password ?? string.Empty);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    /// <summary>Creates the first administrator when none exists. Safe to call on every startup.</summary>
    public async Task EnsureInitialAdminAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.AdminUsers.AnyAsync())
        {
            return;
        }

        var email = InputNormaliser.Email(_configuration["Admin:InitialEmail"]);
        var password = _configuration["Admin:InitialPassword"];
        var generated = false;

        if (email.Length == 0 || string.IsNullOrEmpty(password))
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "No administrator exists. Set Admin:InitialEmail and Admin:InitialPassword " +
                    "(environment variables Admin__InitialEmail / Admin__InitialPassword) and restart.");
                return;
            }

            email = "admin@example.test";
            password = GeneratePassword();
            generated = true;
        }
        else if (password.Length < MinimumPasswordLength)
        {
            _logger.LogWarning("Admin:InitialPassword must be at least {Length} characters. No administrator was created.", MinimumPasswordLength);
            return;
        }

        var admin = new AdminUser { Email = email };
        admin.PasswordHash = _hasher.HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        if (generated)
        {
            // Shown once, only in Development, and only because no password was configured.
            _logger.LogWarning("Development administrator created. Sign in with {Email} and password {Password}", email, password);
        }
        else
        {
            _logger.LogInformation("Administrator {Email} created from configuration.", email);
        }
    }

    private static string GeneratePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(15))
            .Replace('+', 'A').Replace('/', 'B').TrimEnd('=');
}
