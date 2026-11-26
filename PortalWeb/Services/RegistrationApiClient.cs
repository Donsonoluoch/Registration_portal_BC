using System.Net.Http.Json;
using System.Text.Json;
using PortalWeb.Models;

namespace PortalWeb.Services;

public record RegistrationResult(bool Success, string Message);

public class RegistrationApiClient(HttpClient http, ILogger<RegistrationApiClient> log)
{
    public async Task<RegistrationResult> SubmitAsync(RegisterFormModel model, CancellationToken ct = default)
    {
        try
        {
            // ConfirmPassword is a UI concern and is not sent.
            using var response = await http.PostAsJsonAsync("api/registrations", new
            {
                model.Email,
                model.FirstName,
                model.LastName,
                model.Password
            }, ct);

            if (response.IsSuccessStatusCode)
                return new RegistrationResult(true, "Registered.");

            return new RegistrationResult(false, await ReadMessageAsync(response, ct));
        }
        catch (HttpRequestException ex)
        {
            log.LogError(ex, "RegistrationApi is unreachable");
            return new RegistrationResult(false, "The registration service is unavailable. Please try again later.");
        }
        catch (TaskCanceledException ex)
        {
            log.LogError(ex, "RegistrationApi call timed out");
            return new RegistrationResult(false, "The registration service timed out. Please try again.");
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.GetString() is { Length: > 0 } text)
                return text;
        }
        catch (JsonException) { /* fall through */ }

        return response.StatusCode == System.Net.HttpStatusCode.BadRequest
            ? "Please check the details you entered."
            : "Could not complete registration right now.";
    }
}
