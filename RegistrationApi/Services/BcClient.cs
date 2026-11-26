using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using RegistrationApi.Options;

namespace RegistrationApi.Services;

public record BcRegisterPayload(string Email, string FirstName, string LastName, string ExternalUserId);
public record BcResult(bool Success, string Message);

/// <summary>
/// Calls the AL codeunit "Portal Registration Mgt" through Business Central's SOAP endpoint.
/// The JSON payload travels as a single text parameter (requestJson).
/// </summary>
public class BcClient(HttpClient http, IOptions<BcOptions> options, ILogger<BcClient> log)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly BcOptions _o = options.Value;

    public async Task<BcResult> RegisterAsync(BcRegisterPayload p, CancellationToken ct = default)
    {
        XNamespace reg = $"urn:microsoft-dynamics-schemas/codeunit/{_o.ServiceName}";

        var json = JsonSerializer.Serialize(new
        {
            email = p.Email,
            firstName = p.FirstName,
            lastName = p.LastName,
            externalUserId = p.ExternalUserId
        });

        // XElement escapes the JSON for us.
        var envelope = new XDocument(
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", Soap),
                new XElement(Soap + "Body",
                    new XElement(reg + _o.ActionName,
                        new XElement(reg + "requestJson", json)))));

        var url = $"{_o.BaseUrl.TrimEnd('/')}/WS/{Uri.EscapeDataString(_o.Company)}/Codeunit/{_o.ServiceName}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting),
                                        Encoding.UTF8, "text/xml")
        };
        req.Headers.Add("SOAPAction", $"urn:microsoft-dynamics-schemas/codeunit/{_o.ServiceName}:{_o.ActionName}");

        if (_o.AuthMode.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_o.Basic.UserName}:{_o.Basic.Password}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        try
        {
            using var resp = await http.SendAsync(req, ct);
            var xml = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var fault = Find(xml, "faultstring");
                log.LogWarning("BC returned {Status}: {Fault}", (int)resp.StatusCode, fault);
                return new BcResult(false, fault ?? $"Business Central returned HTTP {(int)resp.StatusCode}.");
            }

            var ret = Find(xml, "return_value");
            if (string.IsNullOrWhiteSpace(ret))
                return new BcResult(false, "Business Central returned an empty response.");

            return JsonSerializer.Deserialize<BcResult>(ret, JsonOpts)
                   ?? new BcResult(false, "Could not read Business Central's response.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogError(ex, "Call to Business Central failed");
            return new BcResult(false, "Business Central is currently unreachable.");
        }
    }

    private static string? Find(string xml, string localName)
    {
        try
        {
            return XDocument.Parse(xml).Descendants()
                .FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
