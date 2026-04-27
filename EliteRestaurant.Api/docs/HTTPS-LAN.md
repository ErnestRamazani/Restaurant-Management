# HTTPS on the LAN (self-signed)

Staff tablets should use **HTTPS** so sign-in traffic is not readable on Wi‑Fi. Use a **self-signed** certificate trusted on each tablet (one-time per device).

## 1. Export a PFX on the API host (Windows)

From the solution root (or any folder), run:

```powershell
dotnet dev-certs https --export-path .\EliteRestaurant.Api\certs\elite-lan.pfx --password "YourStrongPassword"
```

- Keep the PFX file **only** on the server; add `certs/*.pfx` to backups with care.
- Set the same password in the environment (see below). Do **not** commit the password.

## 2. Tell Kestrel the password

The API reads the PFX password from (first match):

- Environment variable: `ELITE_LAN_CERTIFICATE_PASSWORD`
- Or config: `LanHttps:CertificatePassword` (e.g. user secrets for local dev only)

Example (PowerShell, session):

```powershell
$env:ELITE_LAN_CERTIFICATE_PASSWORD = "YourStrongPassword"
```

## 3. Bind addresses

By default the API listens on:

- **HTTPS:** `0.0.0.0:7194` (when `certs/elite-lan.pfx` exists)
- **HTTP:** `0.0.0.0:5223` (always; browsers are redirected to HTTPS when HTTPS is enabled)

Configure **CORS** `AllowedOrigins` in `appsettings.json` with each tablet origin, e.g. `https://192.168.1.50:7194`.

## 4. Trust the certificate on each tablet

Each device must **trust** the same certificate (or the connection will show a browser warning / fetch may fail):

1. Copy `elite-lan.pfx` or export the **public** certificate (`.cer`) from the PFX.
2. On the tablet: install the cert into **Trusted Root Certification Authorities** (or **User** trusted roots for testing).
3. Open the portal at `https://<server-lan-ip>:7194/`.

For production hardening, prefer a **dedicated** CA or an internal PKI; self-signed is acceptable for isolated LAN per this deployment model.

## 5. No PFX yet

If `certs/elite-lan.pfx` is missing, the API starts **HTTP-only** on port **5223** and logs a warning. Create the PFX and restart to enable HTTPS.
