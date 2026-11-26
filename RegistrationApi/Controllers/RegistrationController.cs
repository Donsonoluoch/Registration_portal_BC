using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistrationApi.Data;
using RegistrationApi.Models;
using RegistrationApi.Services;

namespace RegistrationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationsController(
    AppDbContext db,
    BcClient bc,
    IConfiguration config,
    ILogger<RegistrationsController> log) : ControllerBase
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    [HttpPost]
    public async Task<ActionResult<RegistrationResponse>> Register(
        [FromBody] RegistrationRequest request, CancellationToken ct)
    {
        if (!ApiKeyIsValid())
            return Unauthorized(new RegistrationResponse(false, "Unauthorized."));

        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new RegistrationResponse(false, "That email is already registered."));

        var user = new AppUser
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            ExternalUserId = Guid.NewGuid().ToString("N"),
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique index hit by a concurrent request with the same email.
            return Conflict(new RegistrationResponse(false, "That email is already registered."));
        }

        // Only profile fields go to Business Central. The password and its hash never leave this service.
        var bcResult = await bc.RegisterAsync(
            new BcRegisterPayload(user.Email, user.FirstName, user.LastName, user.ExternalUserId), ct);

        if (!bcResult.Success)
        {
            // Compensate: undo the local user so both systems stay consistent.
            db.Users.Remove(user);
            await db.SaveChangesAsync(CancellationToken.None);
            log.LogWarning("Registration rolled back for {Email}: {Reason}", email, bcResult.Message);
            return StatusCode(StatusCodes.Status502BadGateway,
                new RegistrationResponse(false, bcResult.Message));
        }

        return Ok(new RegistrationResponse(true, "Registered."));
    }

    private bool ApiKeyIsValid()
    {
        var expected = config["ApiKey"];
        if (string.IsNullOrEmpty(expected) ||
            !Request.Headers.TryGetValue("X-Api-Key", out var provided))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided.ToString()));
    }
}
