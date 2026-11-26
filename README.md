# BC Portal: User Registration

A registration portal for **Microsoft Dynamics 365 Business Central (on-premises, BC260)**.
A visitor fills in email, first name, last name and password. The portal stores the credentials,
and Business Central receives the profile (never the password) through an AL codeunit exposed as a SOAP web service.

---

## 1. Architecture

```
Browser
  |  HTTPS form
  v
PortalWeb (ASP.NET Core MVC)            https://localhost:<web-port>
  |  HTTPS + JSON, header X-Api-Key
  v
RegistrationApi (ASP.NET Core Web API)  https://localhost:7170
  |  - validates input
  |  - hashes the password (PBKDF2, ASP.NET PasswordHasher)
  |  - saves the user in its own SQLite database
  |  - calls Business Central
  |  SOAP over HTTP, Windows (NTLM) auth, JSON inside one text parameter
  v
Business Central BC260                  http://localhost:7047/BC260
  codeunit "Portal Registration Mgt"  ->  table "Portal User"
```

| Layer | Responsibility |
|---|---|
| **PortalWeb (MVC)** | Form, client-side and server-side validation, anti-forgery token, friendly error messages |
| **RegistrationApi (microservice)** | Owns credentials, hashing, duplicate checks, the BC call, rollback if BC fails |
| **AL codeunit** | Parses the JSON, validates, checks for duplicates, inserts the Portal User, returns JSON |

**Design decision (Design A):** credentials live in the portal's database. Business Central stores only the
business profile plus an `External User Id` that links the two records. No password or hash ever reaches BC.

---

## 2. Repository layout

```
bc-portal/
├── README.md
├── al-registration/                    Business Central extension
│   ├── app.json                        created by "AL: Go!"
│   ├── .vscode/launch.json             on-prem BC260, port 7049, Windows auth
│   └── src/
│       ├── PortalUser.Table.al                 table 50100 "Portal User"
│       ├── PortalRegistrationMgt.Codeunit.al   codeunit 50100 (SOAP entry point)
│       ├── PortalReg.PermissionSet.al          permission set 50100 "Portal Reg"
│       └── PortalUserList.Page.al              page 50100 "Portal User List" (optional viewer)
├── RegistrationApi/                    microservice
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/RegistrationsController.cs
│   ├── Services/BcClient.cs            builds and sends the SOAP request
│   ├── Data/AppDbContext.cs            EF Core + SQLite
│   ├── Models/AppUser.cs, RegistrationDtos.cs
│   └── Options/BcOptions.cs
└── PortalWeb/                          MVC site
    ├── Program.cs
    ├── appsettings.json
    ├── Controllers/AccountController.cs
    ├── Models/RegisterFormModel.cs
    ├── Services/RegistrationApiClient.cs
    └── Views/Account/Register.cshtml, RegistrationSuccess.cshtml
```

---

## 3. Prerequisites

| Tool | Notes |
|---|---|
| Business Central on-premises (BC260) | SOAP and developer endpoints enabled on the server instance |
| VS Code + **AL Language** extension | For the BC extension |
| .NET SDK 10 (LTS) | `dotnet --version` |
| PowerShell | Commands below assume Windows PowerShell or PowerShell 7 |
| Postman or PowerShell | For testing the BC endpoint directly |

---

## 4. Setup

### 4.1 Business Central instance

1. Enable SOAP on the server instance (BC Administration tool > instance > Edit > SOAP Services), or in the Administration Shell:
   ```powershell
   Set-NAVServerConfiguration -ServerInstance BC260 -KeyName SOAPServicesEnabled -KeyValue true
   Set-NAVServerConfiguration -ServerInstance BC260 -KeyName SOAPServicesPort -KeyValue 7047
   Restart-NAVServerInstance -ServerInstance BC260
   ```
2. Enable the **developer service endpoint** (port 7049) so F5 can publish.

### 4.2 AL extension

1. Open `al-registration` in VS Code, run **AL: Go!** once, set `idRanges` in `app.json` (this project uses 50100-50149).
2. **AL: Download Symbols**, then **F5** to publish.
3. In BC, open **Web Services** and add a line (the extension does not create it automatically):

   | Field | Value |
   |---|---|
   | Object Type | Codeunit |
   | Object ID | 50100 |
   | Service Name | `PortalRegistrationMgt` (must match `Bc:ServiceName` below) |
   | Published | Yes |
4. Assign the permission set **Portal Reg** to the Windows user that runs RegistrationApi.
5. Verify in a browser:
   - Service list: `http://localhost:7047/BC260/WS/<Company>/services`
   - Schema: `http://localhost:7047/BC260/WS/<Company>/Codeunit/<ServiceName>?wsdl`
   (URL-encode spaces in the company name, for example `Don%20Ent`.)

### 4.3 RegistrationApi

```powershell
cd RegistrationApi
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet user-secrets init
```
Configure `appsettings.json` (section **Bc**, see section 5). Set the shared API key (section 6).

### 4.4 PortalWeb

```powershell
cd PortalWeb
dotnet user-secrets init
```
Set `RegistrationApi:BaseUrl` in `appsettings.json` to the address RegistrationApi prints on startup (section 5).

### 4.5 Shared API key (run ONCE, in ONE PowerShell window)

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

cd <path>\RegistrationApi
dotnet user-secrets set "ApiKey" $key

cd <path>\PortalWeb
dotnet user-secrets set "RegistrationApi:ApiKey" $key
```
Do not run the first line again afterwards: a new run creates a different key. Verify with
`dotnet user-secrets list` in both folders; the values must be identical. Restart both apps after any change.

---

## 5. Configuration reference

**RegistrationApi/appsettings.json**

| Key | Example | Meaning |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=portal.db` | SQLite file for portal users |
| `Bc:BaseUrl` | `http://localhost:7047/BC260` | SOAP base URL: host, SOAP port, server instance. No `/api`, no `/WS` |
| `Bc:Company` | `Don Ent` | Exact company name from BC's Companies page |
| `Bc:ServiceName` | `PortalRegistrationMgt` | Must equal the Service Name on BC's Web Services page |
| `Bc:ActionName` | `RegisterUser` | Name of the global AL procedure |
| `Bc:AuthMode` | `Windows` | `Windows` (default credentials of the process) or `Basic` (web service access key) |
| `ApiKey` | (user-secrets) | Shared secret checked on every request |

**PortalWeb/appsettings.json**

| Key | Example | Meaning |
|---|---|---|
| `RegistrationApi:BaseUrl` | `https://localhost:7170/` | Where RegistrationApi listens (read its startup log) |
| `RegistrationApi:ApiKey` | (user-secrets) | Same value as the API's `ApiKey` |

Secrets (API key, Basic password) belong in user-secrets locally and in a secret store (for example Azure Key Vault) in production, never in the repository.

---

## 6. Running

Start the API first, then the portal, each in its own terminal:

```powershell
cd RegistrationApi; dotnet run --launch-profile https
cd PortalWeb;       dotnet run --launch-profile https
```
Open `https://localhost:<PortalWeb port>/Account/Register`. The port is printed as `Now listening on: ...` in the PortalWeb terminal.
The API port (for example 7170) serves no pages: browsing to it returns 404.

---

## 7. Contracts

### 7.1 RegistrationApi

`POST /api/registrations` with header `X-Api-Key: <key>`

```json
{ "email": "jane@example.com", "firstName": "Jane", "lastName": "Doe", "password": "S3cure!pass" }
```

| Status | Body | Meaning |
|---|---|---|
| 200 | `{ "success": true, "message": "Registered." }` | User saved locally and in BC |
| 400 | validation problem | Invalid input |
| 401 | `{ "success": false, "message": "Unauthorized." }` | Missing or wrong API key |
| 409 | `{ "success": false, "message": "That email is already registered." }` | Duplicate email |
| 502 | `{ "success": false, "message": "<BC message>" }` | BC rejected or is unreachable (local user is rolled back) |

### 7.2 Business Central codeunit (SOAP)

Endpoint: `{BaseUrl}/WS/{Company}/Codeunit/{ServiceName}`
SOAPAction: `urn:microsoft-dynamics-schemas/codeunit/{ServiceName}:RegisterUser`

Request body (the JSON travels as one text parameter, `requestJson`):

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:reg="urn:microsoft-dynamics-schemas/codeunit/PortalRegistrationMgt">
  <soapenv:Body>
    <reg:RegisterUser>
      <reg:requestJson>{"email":"jane@example.com","firstName":"Jane","lastName":"Doe","externalUserId":"..."}</reg:requestJson>
    </reg:RegisterUser>
  </soapenv:Body>
</soapenv:Envelope>
```

The response carries `<return_value>` containing `{"success":true,"message":"Registered"}`
(or `false` with a reason such as `Invalid email` or `Email already registered`).

---

## 8. Data model

| Store | Table | Fields |
|---|---|---|
| Portal (SQLite) | `Users` | Id, Email (unique), FirstName, LastName, PasswordHash, ExternalUserId, CreatedAtUtc |
| Business Central | `Portal User` (50100) | Email (key), First Name, Last Name, External User Id, Created At |

`ExternalUserId` is the same value on both sides and links the records.

---

## 9. Registration flow

1. The visitor submits the form. MVC validates it (including password confirmation and the anti-forgery token).
2. PortalWeb posts the data to RegistrationApi over HTTPS with the API key. The confirm-password field is not sent.
3. RegistrationApi checks the key, normalises the email, rejects duplicates, hashes the password and saves the user.
4. RegistrationApi calls the BC codeunit with email, names and `externalUserId` only.
5. If BC fails, the local user is deleted again so both systems stay consistent, and the visitor sees the reason.

---

## 10. Verifying that users reached Business Central

- Open the table directly: `http://<web-client>/BC260/?company=<Company>&table=50100`
- Or publish `PortalUserList.Page.al` and search **Portal Users** in BC (`Alt+Q`).
- Match the `External User Id` in BC with `ExternalUserId` in `RegistrationApi/portal.db` for the same email.

Test the BC endpoint on its own (PowerShell, Windows auth):

```powershell
$url  = "http://localhost:7047/BC260/WS/Don%20Ent/Codeunit/PortalRegistrationMgt"
$json = '{"email":"test@example.com","firstName":"Test","lastName":"User","externalUserId":"123"}'
$esc  = [System.Security.SecurityElement]::Escape($json)
$body = @"
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:reg="urn:microsoft-dynamics-schemas/codeunit/PortalRegistrationMgt">
  <soapenv:Body><reg:RegisterUser><reg:requestJson>$esc</reg:requestJson></reg:RegisterUser></soapenv:Body>
</soapenv:Envelope>
"@
Invoke-WebRequest -Method Post -UseDefaultCredentials -Uri $url -ContentType "text/xml; charset=utf-8" `
  -Headers @{ SOAPAction = "urn:microsoft-dynamics-schemas/codeunit/PortalRegistrationMgt:RegisterUser" } -Body $body
```

---

## 11. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Could not find the global property 'UserSecretsId'` | Project not initialised | `dotnet user-secrets init` in that project |
| `Missing parameter value for 'value'` | `$key` is empty (new window or lost variable) | Generate the key again and set both secrets in one window |
| Portal shows "Unauthorized." (HTTP 401) | `ApiKey` differs between the two projects, or an app wasn't restarted | Set both from one `$key`, compare with `dotnet user-secrets list`, restart both |
| 404 on `/Account/Register` | Opened on the API's port | Use PortalWeb's own port |
| "registration service is unavailable" | API not running, wrong `RegistrationApi:BaseUrl` port, or untrusted dev certificate | Check the API terminal, fix the port, `dotnet dev-certs https --trust` |
| BC returns HTTP 405 | SOAP disabled on the instance | Enable SOAP, restart the instance |
| BC returns HTTP 404 | Wrong company or service name, or service not published | Compare with `.../WS/<Company>/services` |
| BC returns HTTP 401 | The calling Windows user is not a BC user or lacks the permission set | Add the user, assign **Portal Reg** |
| Codeunit not found on the Web Services page | Publishing the extension does not create the web service entry | Add the line manually (section 4.2) |

The RegistrationApi console shows the exact BC SOAP fault when a call fails.

---

## 12. Security notes

- Passwords are hashed with ASP.NET Core's `PasswordHasher` (salted PBKDF2) in RegistrationApi and are never sent to BC.
- The portal-to-API call uses HTTPS and a shared API key checked in constant time.
- `Basic` auth with a web service access key must only be used over HTTPS.
- Never log passwords or keys. Rotate any key that has been shared outside your machine.
- `EnsureCreated()` creates the SQLite file for development. Use EF Core migrations and a production database before release.

---

## 13. Going live (cloud BC)

- Microsoft has deprecated SOAP in Business Central and will remove it in a future release. Keep the BC call isolated in `BcClient` so it can be replaced by an API page (REST) with little change.
- Use per-environment configuration (BC URL, company, credentials) and a separate Entra ID app registration for SaaS (OAuth 2.0 client credentials, not Basic auth).
- Deploy the extension to production as a per-tenant extension (`.app` upload or pipeline), not through F5.
- Recreate the Web Services entry and assign the permission set in each environment, or automate it in an install codeunit.
- Expect API rate limits in BC online: add retry handling for HTTP 429 and 503.
- Check data-protection obligations for stored user data.

---

## 14. Possible next steps

- Login and password reset against the portal's own database (or an identity provider such as Microsoft Entra External ID).
- Email verification before the user is created in BC.
- Replace the custom Portal User table with Contact or Customer records if the business requires it.
- Automated tests for the codeunit and the controller.
- Cache lookups and add resilience policies (retry, circuit breaker) to `BcClient`.
