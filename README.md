# RentalHive

RentalHive is a peer-to-peer rental marketplace for Sweden. Members rent out cars, vans, minibuses, light trucks, trailers, car transporters, boat trailers, boats and machines. Other members search by category, city, dates and price, then send a booking request. The owner confirms or declines.

Built with .NET 10 (LTS), ASP.NET Core, Blazor and PostgreSQL in a Clean Architecture layout.

## Features

| Area | What you get |
|---|---|
| **Accounts** | Register with personnummer (validated, normalised, 18+), email and mobile number. Log in with any of the three. Lockout after repeated failures, password change that signs you out everywhere, profile page |
| **Listings** | Nine categories, each with its own validated fields (for example load volume for vans, axles for car transporters, boating licence for boats). Up to 8 photos per listing, activate and deactivate, delete (listings with booking history are deactivated instead) |
| **Search** | Category, city, keyword, max price, sorting, pagination. **Availability:** only items free for the whole requested period are shown |
| **Bookings** | Quote, then request (with a message to the owner). Owners confirm or decline, and confirming automatically declines overlapping requests. Cancel before the start, then mark as returned. **No double bookings**, enforced by a PostgreSQL exclusion constraint. Contact details are shared only after confirmation |
| **Reviews** | One review per completed rental (1–5 stars and a comment), with an average rating on each listing |
| **API** | A REST API for mobile and third-party clients, with JWT authentication. It uses the same business logic as the web app |

## Architecture

```
RentalHive.Domain          Entities and business rules (booking lifecycle, pricing, personnummer, phone numbers)
RentalHive.Application     DTOs, service contracts, listing factory and validation
RentalHive.Infrastructure  EF Core (PostgreSQL), services, encryption, password hashing, image storage, migrations
RentalHive.Api             REST API (JWT bearer), OpenAPI in Development
RentalHive.Web             Blazor web app (server-rendered + interactive server), cookie authentication
RentalHive.Tests           Unit tests + API integration tests against a real PostgreSQL database
```

The web app calls the application services in-process; it doesn't call the API over HTTP. Interactive components run each operation in its own DI scope, so a long-lived Blazor circuit never shares one `DbContext` across concurrent calls.

## Getting started (Docker)

```bash
cp .env.example .env     # fill in POSTGRES_PASSWORD, Security__PersonalNumberKey, Jwt__Key
docker compose up -d --build
```

The web app runs on <http://localhost:8080> and the API on <http://localhost:8081>. The web container applies database migrations on startup.

## Development

You need the .NET 10 SDK and PostgreSQL 14 or newer.

```bash
export ConnectionStrings__RentalHive="Host=localhost;Database=rentalhive;Username=rentalhive;Password=…"
export Security__PersonalNumberKey="$(openssl rand -base64 32)"
export Jwt__Key="$(openssl rand -base64 48)"      # API only

dotnet run --project RentalHive.Web     # http://localhost:5070
dotnet run --project RentalHive.Api     # http://localhost:5080  (OpenAPI: /openapi/v1.json)
```

For local secrets you can also use `dotnet user-secrets`. **No secret is stored in a committed file**, and both hosts refuse to start if a required key is missing.

To create a new migration:

```bash
RENTALHIVE_DB="Host=localhost;Database=rentalhive;Username=…;Password=…" \
  dotnet ef migrations add <Name> --project RentalHive.Infrastructure --output-dir Persistence/Migrations
```

### Tests

```bash
TEST_DATABASE_URL="Host=localhost;Database=rentalhive_test;Username=rentalhive;Password=…" dotnet test
```

There are 51 tests: unit tests for the domain rules, encryption, validation and URL/image safety, plus API integration tests that run against a real PostgreSQL. The test database's schema is dropped and recreated on every run, so point `TEST_DATABASE_URL` at a dedicated database. CI also builds both Docker images and fails on high or critical vulnerable packages.

## Security

- **Personnummer:** stored encrypted (AES-256-GCM) together with a keyed HMAC-SHA256 hash, so lookups and uniqueness checks work without decrypting. Both keys are derived with HKDF from `Security:PersonalNumberKey`. It is shown only to its owner, masked (`19811218-****`), and never appears in listings or bookings.
- **Passwords:** BCrypt with a work factor of 12. Unknown users take the same time as wrong passwords, so login timing doesn't reveal which accounts exist. Accounts lock after 5 failed attempts, and the credential endpoints are rate-limited.
- **Sessions:** the web app uses an `HttpOnly`, `Secure` cookie with antiforgery on all forms, including logout. The API uses short-lived JWTs that are fully validated (issuer, audience, lifetime, HS256 only). A per-user security stamp revokes every cookie and token when the password changes, and interactive sessions are re-checked every 10 minutes.
- **Authorization:** the acting user always comes from the session or token, never from the request body. Non-owners get `404` for other members' listings and bookings, so IDs can't be probed.
- **Bookings:** overlapping confirmed bookings are impossible at the database level (`EXCLUDE USING gist` on `daterange`). Concurrent edits are caught with optimistic concurrency (`xmin`).
- **Uploads:** file types are checked by content (JPEG, PNG or WebP), at most 5 MB each. Files get random names and are served with `nosniff`.
- **Headers and input:** a strict Content-Security-Policy and other security headers, HSTS in production, local-only redirects after login, escaped `LIKE` searches, and ProblemDetails error responses.

## Configuration

| Setting | Purpose |
|---|---|
| `ConnectionStrings__RentalHive` | PostgreSQL connection string |
| `Security__PersonalNumberKey` | Base64 32-byte master key. **Back it up**: losing it makes stored personnummer unreadable |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` / `Jwt__LifetimeMinutes` | API token settings |
| `Storage__ImageDirectory` | Where listing photos are stored (a volume in Docker) |
| `DataProtection__KeysPath` | Cookie encryption keys; must persist across restarts and be shared by all web instances |
| `Cors__Origins__N` | Browser origins allowed to call the API |
| `ForwardedHeaders__KnownProxies__N` | Reverse-proxy IPs to trust for client IP and scheme |
| `Database__MigrateOnStartup` | Apply migrations automatically (default `true`) |

## Not included yet

- Online payment and deposits (for example Swish or Stripe). Renters and owners currently settle directly after confirmation.
- Email or SMS notifications for new requests and confirmations.
- In-app messaging between renter and owner.
- BankID verification of personnummer. Format and checksum are validated, but identity is not verified.
- Insurance, damage reports and admin moderation tools.
