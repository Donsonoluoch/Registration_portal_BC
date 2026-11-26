using System.ComponentModel.DataAnnotations;

namespace RegistrationApi.Models;

/// <summary>The portal's own user record. Credentials live here, not in Business Central.</summary>
public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    /// <summary>Links this user to the record created in Business Central.</summary>
    [Required, MaxLength(50)]
    public string ExternalUserId { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
