# DigitalOcean Cloud Deployment

This repo can be deployed as a single Dockerized API + web hub service on DigitalOcean App Platform, backed by DigitalOcean Managed PostgreSQL.

## 1. Create Managed PostgreSQL

Create a PostgreSQL cluster in DigitalOcean and copy the connection string.

Use SSL-required connection strings in production, for example:

```text
Host=<host>;Port=25060;Database=defaultdb;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

## 2. Configure App Platform

Use `.do/app.yaml` as the starter App Platform spec. Replace placeholders:

- `ELITE_POSTGRES_CONNECTION`
- `JWT__Issuer`
- `JWT__SigningKey`
- `Cors__AllowedOrigins__0`

DigitalOcean provides a public `https://...ondigitalocean.app` URL until a custom domain is added.

## 3. Build Behavior

The Dockerfile:

1. Installs and builds `elite-menu`.
2. Copies the built web app into `EliteRestaurant.Api/wwwroot/menu`.
3. Publishes `EliteRestaurant.Api`.
4. Runs the API in the ASP.NET runtime image.

The API reads the platform `PORT` environment variable and listens on that port in cloud hosting.

## 4. Database Migrations

The API currently runs EF Core migrations on startup through `DatabaseInitializer.Initialize()`.

For the first deployment, use a PostgreSQL user that can run migrations. After the schema is stable, split privileges:

- migration/admin role for schema changes
- runtime role for normal application reads/writes

## 5. Health Check

Use:

```text
/api/health
```

Do not expose `/api/health/db` publicly as an operational dashboard; it is only a smoke check.

## 6. WPF Admin Configuration

In the WPF app settings, configure:

```json
{
  "CloudApi": {
    "BaseUrl": "https://your-app.ondigitalocean.app",
    "AccessToken": "",
    "TokenExpiresAtUtc": null
  }
}
```

The long-term goal is for WPF to sign in through the API and store only the API token locally, not a database password.
