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
| **Email notifications** | The owner is emailed about new requests. The renter is emailed when a request is confirmed (with the owner's contact details), declined or no longer available. The other party is emailed about cancellations, and the renter is invited to review once the item is returned. Emails are queued and sent in the background, so a slow mail server never delays the site |
| **Moderation** | Members can report a listing. Moderators review reports, remove listings for good (the owner can't reactivate them), and suspend or reinstate members. Suspension blocks sign-in, ends web sessions and API tokens, hides the member's listings and cancels their pending requests. Everyone affected gets an email |
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

## Deploy to your own server (CI/CD with Docker)

Every push to GitHub runs the tests and, if they pass, publishes the Docker images to GitHub Container Registry:

| Tag | Points to |
|---|---|
| `latest` | the newest build of the default branch |
| `<branch>` | the newest build of a branch, e.g. `claude-charming-noether-lhy664` (`/` becomes `-`) |
| `sha-<7 chars>` | one exact commit |
| `1.2.3` | a `v1.2.3` git tag |

Images: `ghcr.io/onenetwork-tech/rentalhive-web` and `ghcr.io/onenetwork-tech/rentalhive-api`.

The server needs nothing except Docker with the Compose plugin. Nothing is built there.

**Quick test on your own computer.** In a clone of this repository:

```bash
cd deploy
./setup-local.sh      # creates .env with random secrets
BUILD=1 ./deploy.sh   # builds the image on your computer (first time: a few minutes) and starts it
```

Or run `./deploy.sh` without `BUILD=1` to download the image CI already built. That needs a one-time `docker login ghcr.io` with a GitHub token that has `read:packages`, while the packages are private.

You need Docker Desktop (or Docker Engine with Compose), running before you start. On Windows, run the commands in **Git Bash** (comes with Git for Windows) or WSL, not in PowerShell or cmd.

Then open http://localhost:8081 (API on port 8082). To try moderation, make yourself a moderator after registering: `docker compose exec web dotnet RentalHive.Web.dll make-admin you@example.com`. Notification emails appear in `docker compose logs web`. To start over, run `docker compose down -v && rm .env`.

**One-time setup on the server**

1. Log in to the registry with a GitHub token that has `read:packages`. Packages are private unless you make them public under the repository's *Packages*.

   ```bash
   docker login ghcr.io -u <your-github-username>
   ```

2. Copy the deploy files from this repository and create the settings file:

   ```bash
   mkdir -p ~/apps/rentalhive && cd ~/apps/rentalhive
   # copy deploy/docker-compose.yml, deploy/deploy.sh and deploy/.env.example here, then:
   cp .env.example .env
   ```

3. Fill in `.env`. Generate the secrets with `openssl` as the comments say, and set `IMAGE_TAG` to `latest` or to a branch tag.

**Deploy or update**

```bash
./deploy.sh                # pull IMAGE_TAG from .env and restart
./deploy.sh sha-1a2b3c4    # roll forward or back to one exact build
```

`deploy.sh` pulls the image, restarts the stack, waits until the web app and the API reports healthy, and prints the logs if it doesn't. Data is kept in Docker volumes across updates. To start from scratch, run `docker compose down -v`, which deletes all data.

**Plain HTTP on a LAN.** The app is HTTPS-only in production by default. When you test it as `http://<server-ip>:<port>`, keep `Security__HttpsOnly=false` in `.env` as the example has it, or login won't work. Once a TLS reverse proxy (Caddy, Traefik, nginx) is in front, switch it back.

**Going public with HTTPS.** `deploy/Caddyfile.example` puts [Caddy](https://caddyserver.com) in front of the app. Caddy gets and renews Let's Encrypt certificates automatically.

1. Point the domain's DNS A/AAAA record at the server and open ports 80 and 443.
2. Install Caddy, replace `rentalhive.example.se` in the example, copy it to `/etc/caddy/Caddyfile`, then run `sudo systemctl reload caddy`.
3. In `.env`, turn on the settings under "Behind an HTTPS reverse proxy": `Security__HttpsOnly=true`, `ForwardedHeaders__KnownNetworks__0=172.16.0.0/12` and `BIND_ADDRESS=127.0.0.1`. Then run `./deploy.sh`. The `KnownNetworks` setting matters here: without it the app doesn't know requests arrive over HTTPS, and pages with forms fail with an error.

`BIND_ADDRESS=127.0.0.1` means only Caddy can reach the app, so plain `http://<server-ip>:8081` stops working.

**Backups.** `./backup.sh` saves the database, listing photos, the cookie keys, and `.env` (which holds `Security__PersonalNumberKey`: without it the stored personnummer can't be read) to `backups/<timestamp>/`, and keeps the newest 14.

- Schedule it nightly with cron, for example `15 3 * * * cd ~/apps/rentalhive && ./backup.sh >> backups/backup.log 2>&1`.
- Copy the `backups` folder off the server.
- The backups contain secrets and personal data, so keep them private.

To restore onto the same or a new server:

```bash
cp backups/<timestamp>/env .env    # only on a new server
docker compose up -d db
./restore.sh backups/<timestamp>
```

`restore.sh` replaces the current data, so it asks you to confirm first.

**Automatic deploys (optional).** CI can deploy every push of one branch to your server:

1. On the server, install a [self-hosted GitHub Actions runner](https://docs.github.com/actions/hosting-your-own-runners) for this repository and give it the label `local-server`. The runner's user must be allowed to run `docker`.
2. Create `~/apps/rentalhive/.env` on the server once, as above.
3. In the repository's *Settings → Secrets and variables → Actions → Variables*, set `LOCAL_DEPLOY_BRANCH` to the branch to deploy, for example `main`. Set `LOCAL_DEPLOY_DIR` too if you use a different folder.

The `deploy-local` job then runs `deploy.sh` with the exact image CI just built (`sha-…`). It is skipped when `LOCAL_DEPLOY_BRANCH` isn't set.

**Running all four apps on one server.** The deploy files use different ports, so SalmanPlex, BoklyAI, RentalHive and Nikah Noor can run side by side:

| App | Port(s) | Health check |
|---|---|---|
| SalmanPlex | 3001 | `/api/health` |
| BoklyAI | 3002 | `/api/health` |
| RentalHive | 8081 (web), 8082 (API) | `/health` |
| Nikah Noor | 8083 | `/health` |

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

There are 59 tests: unit tests for the domain rules, encryption, validation and URL/image safety, plus API integration tests that run against a real PostgreSQL. The test database's schema is dropped and recreated on every run, so point `TEST_DATABASE_URL` at a dedicated database. CI also builds both Docker images and fails on high or critical vulnerable packages.

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
| `Email__Host` / `Port` / `Security` / `Username` / `Password` / `From` / `FromName` | SMTP for notifications. Without `Email__Host`, nothing is sent |
| `App__PublicUrl` | Public address of the web app, for links in emails |
| `ForwardedHeaders__KnownNetworks__N` | Proxy networks to trust, in CIDR form (e.g. `172.16.0.0/12` for a proxy on the Docker host) |

## Moderators

Only someone with access to the server can make a moderator. The account must already be registered, and they need to sign in again afterwards to see **Moderation** in the menu:

```bash
cd ~/apps/rentalhive
docker compose exec web dotnet RentalHive.Web.dll make-admin someone@example.se
docker compose exec web dotnet RentalHive.Web.dll remove-admin someone@example.se
```

Moderator rights aren't granted by email setting, because email addresses aren't verified: anyone could register an address first.

## Not included yet

- Online payment and deposits (for example Swish or Stripe). Renters and owners currently settle directly after confirmation.
- SMS notifications. Email notifications are included.
- In-app messaging between renter and owner.
- BankID verification of personnummer. Format and checksum are validated, but identity is not verified.
- Insurance and damage reports.
- Queued emails are kept in memory, so emails waiting to be sent when the app restarts are lost.
