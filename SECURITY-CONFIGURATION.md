# Application configuration and secrets

The API and MVC projects each have a `UserSecretsId`. In Development, `WebApplication.CreateBuilder` loads that project's .NET User Secrets automatically. Set values separately for both startup projects when both processes need them:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local connection string>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Jwt:Key" "<long random signing key>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Email:Smtp:Username" "<SMTP username>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Email:Smtp:Password" "<SMTP password>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Email:Smtp:From" "<sender address>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Paystack:SecretKey" "<Paystack secret key>" --project Src/Clean-Connect.Api
dotnet user-secrets set "Geocoding:ApiKey" "<geocoding API key>" --project Src/Clean-Connect.Api
dotnet user-secrets set "AgoraChat:AppId" "<Agora app ID>" --project Src/Clean-Connect.Api
dotnet user-secrets set "AgoraChat:AppKey" "<Agora app key>" --project Src/Clean-Connect.Api
dotnet user-secrets set "AgoraChat:AppCertificate" "<Agora app certificate>" --project Src/Clean-Connect.Api
```

Repeat for `Src/Clean-Connect.Web` for settings it consumes. Configure `Jwt:Issuer` and `Jwt:Audience` in that project's Development settings, and set the API's `Cors:AllowedOrigins` to the actual local frontend origin.

Optional local integrations use User Secrets too: `Admin:SetupKey` enables the initial administrator setup flow, and `Authentication:Google:ClientId` plus `Authentication:Google:ClientSecret` enable Google sign-in. Configure both Google values together.

In Production, inject settings using the deployment platform's secret manager or environment variables. Nested keys use double underscores, for example `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Email__Smtp__Password`, `Paystack__SecretKey`, `Geocoding__ApiKey`, `AgoraChat__AppCertificate`, and `Cors__AllowedOrigins__0`. Configure all required values for both API and MVC. Startup validation reports missing key names only and fails before services are used.

Do not put values in tracked `appsettings*.json`, source code, test output, or logs. The development values previously present in this workstation's local configuration were migrated to User Secrets. On a fresh clone or another workstation, configure the required User Secrets before running the application in Development.
