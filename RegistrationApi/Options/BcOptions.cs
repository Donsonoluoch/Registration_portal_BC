namespace RegistrationApi.Options;

public class BcOptions
{
    public const string SectionName = "Bc";

    /// <summary>SOAP base incl. server instance, e.g. http://localhost:7047/BC260</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Exact company name as shown on BC's Companies page.</summary>
    public string Company { get; set; } = "";

    /// <summary>Service Name used on BC's Web Services page.</summary>
    public string ServiceName { get; set; } = "PortalRegistration";

    /// <summary>Name of the global AL procedure in the codeunit.</summary>
    public string ActionName { get; set; } = "RegisterUser";

    /// <summary>Windows | Basic</summary>
    public string AuthMode { get; set; } = "Windows";

    public BasicAuthOptions Basic { get; set; } = new();
}

public class BasicAuthOptions
{
    public string UserName { get; set; } = "";

    /// <summary>Web service access key. Keep it in user-secrets, never in appsettings.json.</summary>
    public string Password { get; set; } = "";
}
