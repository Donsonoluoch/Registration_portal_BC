using System.ComponentModel.DataAnnotations;

namespace RegistrationApi.Models;

public class RegistrationRequest
{
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    [Required, StringLength(50)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50)]
    public string LastName { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
}

public record RegistrationResponse(bool Success, string Message);
