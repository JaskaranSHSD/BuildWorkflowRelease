# BuildWorkflowRelease

ASP.NET Core (net10) web app that orchestrates: pull latest changes from a GitHub (or any git) repository, run a local build (SDK assumed installed), and upload artifacts to an SFTP server (password or private-key auth). System configuration is stored in PostgreSQL via EF Core.

The entire app is protected by an authentication layer. Users must sign in before accessing any page. A local (seeded) account is provided for development, and an SSO flow is available for identity-provider based login.

## Setup

1. Ensure .NET 10 SDK, git, and (optionally) your project build SDKs are installed.
2. Create a PostgreSQL database / user, then update `appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=buildworkflow;Username=postgres;Password=postgres"
}
```

3. Restore & run:

```bash
dotnet restore
dotnet run
```

The schema is created automatically on startup (`EnsureCreated`), including the `Users`, health-check, repository, build, SFTP, profile and release tables. Lightweight `ALTER`/`CREATE TABLE IF NOT EXISTS` statements keep existing databases up to date.

## Authentication

All routes require an authenticated user (global fallback authorization policy). The login pages (`/Account/Login`, `/SSO/*`) are anonymous.

### Seed user

On first startup, when the `Users` table is empty, a default administrator account is seeded:

| Field    | Value                 |
|----------|-----------------------|
| Username | `admin`               |
| Email    | `admin@example.com`   |
| Password | `Admin@123`           |

Log in with username or email at `/Account/Login`. Sessions use an ASP.NET Core cookie (sliding expiration). Sign out via the logout button in the header.

### Password storage

Passwords are hashed with `Microsoft.AspNetCore.Identity.PasswordHasher<User>` and stored in the `PasswordHash` column. No plaintext passwords are stored.

### User table

`Models/Entities/User.cs` / table `Users`:

| Column         | Type     | Notes                          |
|----------------|----------|--------------------------------|
| Id             | int (PK) |                                |
| Username       | text     | Unique                         |
| Email          | text     | Unique                         |
| PasswordHash   | text     | Hashed password                |
| DisplayName    | text     | Optional                       |
| IsActive       | bool     | Account enabled flag           |
| CreatedAt      | timestamptz |                             |
| LastLoginAt    | timestamptz | Updated on each sign-in      |

## SSO (Single Sign-On)

`SSOController` implements a token-based SSO entry point:

- `GET/POST /SSO/Callback?token=...` – receives an SSO token.
- Validates the token by calling the identity-provider API (stubbed in `SsoService.ValidateTokenAsync`; implement the HTTP call there).
- Resolves user details (email, username, display name) from the API response.
- Verifies the resolved details match the email identity.
- If no local user with that email exists, creates one (SSO-only, random unusable password).
- If the user exists, updates non-conflicting fields (display name / username) and signs in.
- Signs the user in with a cookie and redirects to the originally requested URL or the home page.

Testing without a configured provider:
- `/SSO/Login` opens a token-entry form.
- With `Sso:UserInfoEndpoint` empty, validation returns failure and the user is redirected to `/Account/Login` with an error.

### Configuration

```json
"Sso": {
  "UserInfoEndpoint": "",  // identity provider user-info URL (e.g. https://idp.example.com/userinfo)
  "LoginUrl": ""          // optional: external IdP login URL (redirects back to /SSO/Callback)
}
```

The identity-provider call sends the token as `Authorization: Bearer <token>` and parses `email`/`mail`/`upn`, `username`/`preferred_username`, `name`/`displayName` and `id`/`sub` from the JSON response. Adjust parsing in `Services/SsoService.cs` for your provider.

## Usage

- **Repositories**: add git repo URL, branch, local clone path, optional PAT/username.
- **Builds**: bind to a repository, set build command (e.g. `dotnet build -c Release`), output path and artifact glob.
- **SFTP**: target host/port/user, password and/or private key path (+passphrase), remote path.
- **Profiles**: combine a build config + sftp config, toggle auto pull/build/upload.
- **Releases**: click "Run Release" on a profile; follow live logs (auto-refreshes while running).

Secrets (tokens/passwords/passphrases) are protected at rest using ASP.NET Data Protection.
## Security notes

- All routes require an authenticated user (global fallback authorization policy). Login/SSO callback routes are explicitly anonymous.
- Cookie authentication is configured with `HttpOnly`, `SameSite=Lax`, and `Secure` in non-development environments. HSTS and HTTPS redirection are enabled outside Development.
- Anti-forgery tokens are validated automatically on unsafe HTTP methods (`AutoValidateAntiforgeryToken`). SSO callback is exempt where required for identity-provider POSTs.
- Basic hardening headers are emitted on every response (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`).
- Database secrets are protected at rest with ASP.NET Data Protection through `SecretService`.
- The default `admin` account is seeded only in Development. Production deployments must provide users through SSO or another provisioning mechanism.
- Health-check URLs/restart/notify endpoints are validated to be absolute `http`/`https` URLs to reduce SSRF risk.
- The build runner executes configured shell/build commands. Treat configured build commands, repository branches, and remote command input as trusted administrator-only input; only grant accounts to trusted operators.
- Build and remote command features can execute arbitrary code/commands on the host and remote systems by design. Restrict access accordingly.

## Repository storage

- Repository clones are stored under the system temporary directory (`%TEMP%/BuildWorkflowRelease/repos/...`) by default to keep source data out of the application directory.
- Use **Clean Data** on a repository (or **Clean All Repo Data**) to remove cloned repository folders. Cleanup only deletes paths located under the system temp directory.
