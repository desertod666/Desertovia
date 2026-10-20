> **Дезертовия 1.0, D-104:** начата реализация в отдельной тестовой среде.
> [Локальный запуск и VS Code](docs/dezertovia/DEVELOPMENT_1_0.md),
> [единый план](docs/dezertovia/UNIFIED_PLAN_1_0.md),
> [прогресс](docs/dezertovia/IMPLEMENTATION_PROGRESS_1_0.json).
> Версионные записи прежних выпусков ниже — история.

**Дезертовия0.7.16 установлена и отдельно проверена19октября.** [Изменения и квитанции](docs/dezertovia/RELEASE_0_7_16.md). Прогресс сохранён; актуальная новостная редакция батюшки не заменялась.

> **LIVE0.7.13:** [установлена и независимо проверена](docs/dezertovia/RELEASE_0_7_13.md), install39133438404/check39133722743 SUCCESS;139профилей сохранены. Исправлены Dungeonresponse и Androidresourceicons.

# Desertod

Audience village game for streamers. Game rules: [docs/game-design.md](docs/game-design.md). Progress and plans: [docs/roadmap.md](docs/roadmap.md). How to contribute: [CONTRIBUTING.md](CONTRIBUTING.md).

[![CI](https://github.com/desertod666/Desertovia/actions/workflows/ci.yml/badge.svg)](https://github.com/desertod666/Desertovia/actions/workflows/ci.yml)

## Дезертовия: технический план адаптации

Инженерия этого проекта сохраняется как основа нашей версии. Карта правил,
сопоставление модулей, решения владельца, предложения зрителей и план переноса:
[docs/dezertovia/README.md](docs/dezertovia/README.md).
Текущая выпущенная версия — **Дезертовия0.5.1**. Изменения игры уже находятся в
`main`: Telegram-вход, один игровой мир, Срамослав, ночные здания, пилот
стройплощадки хижины, режимы чата и уведомления, компактная кнопка без перекрытия
меню, жители/онлайн наверху «Города» и версия в настройках.
Проверенный выпуск и его точный исходник: [описание0.5.1](docs/dezertovia/CHAT_MODES.md).
Полный текущий план и оставшиеся задачи — в технической памяти выше; не все
пункты планов уже реализованы. Исходная инженерная основа автора:
[buhaiov-vs/Desertovia](https://github.com/buhaiov-vs/Desertovia).

## Structure

| Path | Content |
|---|---|
| `src/Desertod.Domain` | Game rules: stats, roles, tasks catalog, progression, energy, cycle engine. No dependencies. |
| `src/Desertod.Infrastructure` | Entity Framework Core + PostgreSQL, account linking, migrations. |
| `src/Desertod.Api` | ASP.NET Core: authentication, HTTP endpoints, SignalR hub, background cycle settlement. |
| `tests/Desertod.Domain.Tests` | Unit tests for the game math. |
| `web` | React + PixiJS frontend (Vite). Production build goes to `src/Desertod.Api/wwwroot`. |

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24, Docker.

```powershell
docker compose up -d
dotnet run --project src/Desertod.Api            # http://localhost:5080, applies migrations in Development
cd web; npm install; npm run dev                 # http://localhost:5190, proxies /api, /auth, /hubs to the backend
```

Open http://localhost:5190. In Development a name-only login is available for testing.

Pages (everything except signing in needs an account; there are no anonymous visitors):

- `/` — sign in, list your villages, browse open villages.
- `/new` — streamers found a village: name, optional description, open or invite-only. The owner of an invite-only village finds the invite link and code in Settings and can replace the code; `/v/{slug}?invite={code}` fills it in for viewers.
- `/v/{slug}` — citizen page: asks to sign in first, then create a character, pick tasks, renew every few minutes.

### Bots

`tools/Desertod.Bots` fills a village with citizens that play like casual players, so the map has something going on. Each bot signs in through the development login, creates a character, joins the village and then keeps working: it renews when the cycle ends, carries what it gathered to storage (drops it when storage is full), works on construction sites, researches the chosen technology, votes in open polls, and otherwise picks a random basic task. Tasks the village is not ready for are skipped for 10 minutes.

```powershell
dotnet run --project tools/Desertod.Bots -- --village <slug> --count 5
```

| Option | Default |
|---|---|
| `--village` | the open village with the most citizens |
| `--count` | `5` |
| `--invite` | none; needed for an invite-only village |
| `--base` | `http://localhost:5080` (the API in Development, not the Vite server) |
| `--prefix` | `bot`; accounts are `bot1`, `bot2`, … and the same names rejoin the same citizens on the next run |

Bots only build what the Tsar plans: in a new village they drop their loads until the Tsar places a storage blueprint. Ctrl+C stops them; their citizens stay in the village.

## Run everything in Docker

The full stack (PostgreSQL, migrations, application, Caddy reverse proxy with automatic HTTPS) starts with one command. This is also how the production server runs.

```powershell
Copy-Item deploy/.env.example deploy/.env   # then fill in deploy/.env
docker compose -f deploy/docker-compose.yml up -d --build
```

- `DESERTOD_DOMAIN=http://localhost` serves the game at http://localhost without a certificate; a public host name gets HTTPS automatically.
- `DATA_PROTECTION_KEY` signs login cookies. Keep it unchanged between deploys; a new value signs everyone out. Outside Development the application refuses to start without it.
- The application applies database migrations at startup; if a migration fails, it does not start.
- Only Caddy publishes ports (80 and 443). PostgreSQL is on an internal network without internet access.
- Stop with `docker compose -f deploy/docker-compose.yml down`. Data stays in Docker volumes; `down -v` deletes it.

### Redeploy from main

`/opt/desertod` on the server is a checkout of `main`. A restricted SSH key can redeploy it and nothing else: whatever command the caller sends, the server runs `/usr/local/bin/desertod-redeploy` (a root-owned copy of `deploy/redeploy-main.sh`), which fetches `main` and runs `docker compose up -d --build`.

```powershell
ssh -i $HOME\.ssh\desertod-redeploy deploy@91-98-8-14.sslip.io
```

Give someone redeploy rights (run from the repository root with your own unrestricted key; the `.pub` file is their public key):

```powershell
scp deploy/redeploy-main.sh deploy/setup-redeploy.sh $HOME\.ssh\desertod-redeploy.pub deploy@91-98-8-14.sslip.io:/tmp/
ssh deploy@91-98-8-14.sslip.io "sh /tmp/setup-redeploy.sh /tmp/desertod-redeploy.pub"
```

- The first run creates the server's read-only GitHub key and prints it; add it under the repository's Settings → Deploy keys and run the script again.
- Run the script again after changing `deploy/redeploy-main.sh`: the server keeps its own copy, so a merged change does not alter what restricted keys can do until the owner installs it.
- Files that are tracked in git are overwritten on every redeploy; keep server settings in `deploy/.env` only.
- Take the rights back by deleting the key's line from `~/.ssh/authorized_keys` on the server.

### Database backups

The `postgres-backup` service dumps the database every day at `BACKUP_TIME` (UTC) into `BACKUP_DIR` (default `deploy/backups/`) as `desertod-YYYY-MM-DD_HHMM.dump` and deletes dumps older than `BACKUP_KEEP_DAYS` (default 30). The folder is on the server disk, so the Hetzner server backup carries the dumps too; set `BACKUP_TIME` before the Hetzner backup window.

- Dumps stay on the same server. Losing the server or the Hetzner account loses them as well. Keep `deploy/.env` in a password manager: the database password and `DATA_PROTECTION_KEY` are not in the dumps.
- Make a dump now: `docker compose -f deploy/docker-compose.yml exec postgres-backup sh /backup/backup.sh now`
- See past runs and failures: `docker compose -f deploy/docker-compose.yml logs postgres-backup`

Restore over the live database (players are disconnected while the application is stopped):

```powershell
docker compose -f deploy/docker-compose.yml stop api
docker compose -f deploy/docker-compose.yml exec postgres-backup pg_restore --clean --if-exists --no-owner --dbname=desertod /backups/desertod-2026-10-13_0200.dump
docker compose -f deploy/docker-compose.yml start api
```

To look at old data without touching the game, restore into a separate database: `createdb desertod_restore`, then the same `pg_restore` with `--dbname=desertod_restore` (both through `exec postgres-backup`). Drop it afterwards with `dropdb desertod_restore`.

## Login providers

### Email and password

Always available, no outside service involved. Players register with an email, a password (8 to 128 characters) and a display name, then sign in with the email and password.

- One account per email. Emails are trimmed and compared case-insensitively. Nothing is sent to the address, so ownership is not verified and there is no password reset yet.
- One IP address may register only `AccountsPerAddress` accounts within `AddressWindow`. IPv6 addresses are grouped by their /64 network, because one household usually gets a whole /64. The address is stored with the login (`PasswordLogins.RegisteredFrom`).
- Registration and sign-in are limited to `AttemptsPerMinute` requests per address.
- Passwords are hashed with PBKDF2-HMAC-SHA512, 210 000 iterations.
- Behind Caddy the client address comes from forwarded headers (`ASPNETCORE_FORWARDEDHEADERS_ENABLED` in `deploy/docker-compose.yml`). Without them every player shares the proxy's address and the limits apply to everyone together.

| Setting | Default |
|---|---|
| `Authentication:Password:Enabled` | `true` |
| `Authentication:Password:AccountsPerAddress` | `3` (`0` turns the limit off) |
| `Authentication:Password:AddressWindow` | `1.00:00:00` (one day) |
| `Authentication:Password:AttemptsPerMinute` | `10` |

### Third-party providers

Secrets are stored with user secrets (never in `appsettings.json`). Providers without configuration are hidden.

```powershell
cd src/Desertod.Api
dotnet user-secrets set "Authentication:Twitch:ClientId" "<id>"
dotnet user-secrets set "Authentication:Twitch:ClientSecret" "<secret>"
dotnet user-secrets set "Authentication:Google:ClientId" "<id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets set "Authentication:Telegram:BotUsername" "<bot username without @>"
dotnet user-secrets set "Authentication:Telegram:BotToken" "<token from BotFather>"
```

| Provider | Where to register | Redirect / domain |
|---|---|---|
| Twitch | https://dev.twitch.tv/console/apps | `http://localhost:5190/signin-twitch` |
| YouTube (Google sign-in) | Google Cloud Console → Credentials → OAuth client ID (web application) | `http://localhost:5190/signin-google` |
| Telegram | BotFather: create a bot, then `/setdomain` | Telegram does not accept `localhost`; use a public domain or a tunnel |

Notes:

- YouTube login is Google sign-in; it identifies the person, not their channel membership. Membership checks need the YouTube Data API (not implemented yet).
- Twitch subscription checks are not implemented yet either: currently any signed-in account can join any village.

## Languages

English is the default; Russian is also available. Players switch with the language selector in the top bar (the choice is saved in the browser). Any page also accepts `?lang=ru`.

- All player-facing text lives in `web/src/i18n`. `en.ts` is the source of truth; other dictionaries are typed against it, so a missing key is a compile error.
- The server stays language-neutral: catalog names are English, and rule errors return a `code` plus `arguments` in the problem details (`detail` is the English fallback). The client translates task, role, project and resource names by identifier.
- To add a language: create `web/src/i18n/<code>.ts` typed as `Dictionary`, then register it in `DICTIONARIES` and `LANGUAGES` in `translator.ts`.

## Database migrations

```powershell
dotnet tool restore
dotnet ef migrations add <MigrationName> --project src/Desertod.Infrastructure --startup-project src/Desertod.Api --output-dir Data/Migrations
```

## Tests

| Suite | What it covers | Needs |
|---|---|---|
| `tests/Desertod.Domain.Tests` | Game math. | — |
| `tests/Desertod.Api.Tests` | API smoke tests: the real app on a throwaway PostgreSQL container — what needs a login, login, founding a village, joining as a citizen, starting a task, the SignalR hub. | Docker |
| `web/e2e` | Browser smoke tests (Playwright, desktop and phone): every page, every menu tab, the inventory, login and character creation; fails on any uncaught script error. | `docker compose up -d` |

```powershell
dotnet test Desertod.slnx

cd web
npx playwright install chromium   # once
npm run test:e2e                  # or npm run test:e2e:ui to watch them run
```

The browser tests start their own backend (port 5181, database `desertod_e2e`, created automatically) and a production build of the client under `vite preview` (port 5191), so a running development setup is left alone. Set `DESERTOD_E2E_CONNECTION` to use another database. Failures leave screenshots and traces in `web/test-results`; `npx playwright show-report` opens the report.

All three suites, the frontend lint and build, and a Docker image build run on every pull request and every push to `main` ([.github/workflows/ci.yml](.github/workflows/ci.yml)).

While `dotnet run` holds `src/Desertod.Api/bin`, build the .NET tests elsewhere: `dotnet test Desertod.slnx --artifacts-path $env:TEMP\desertod-test-build`.
