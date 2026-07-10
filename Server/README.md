# Highscores server - Docker

Run the Wolfrender highscores API in Docker with persistent data on the host and optional Cloudflare Tunnel exposure.

## Layout

| Path | Purpose |
|------|---------|
| `data/appsettings.json` | Runtime config (CORS origins, log levels). Mounted at `/data/appsettings.json` in the container. |
| `data/highscores.json` | Leaderboard storage. Created automatically on first score if missing. |
| `.env` | Local secrets and ports (`TUNNEL_TOKEN`, `HIGHSCORES_PORT`, `TZ`). |

Copy the score file template before first run:

```bash
cp data/highscores.json.example data/highscores.json
```

## Quick start (local only)

From `Server/`:

```bash
cp .env.example .env
cp data/highscores.json.example data/highscores.json
docker compose up -d --build
```

API: `http://localhost:5080/api/scores/{levelId}`

Recordings: `POST http://localhost:5080/api/recordings` (JSON body with `name` and `recording` payload). Files are stored under `data/recordings/` when using Docker.

Download a highscore recording: `GET http://localhost:5080/api/scores/{levelId}/recordings/{rank}` (returns `{rank}.rec` for the playthrough at that leaderboard position).

Sync recording flags with on-disk files: `POST http://localhost:5080/api/recordings/sync`.

## Discord score announcements

When a score is accepted, the server can post an embed to a Discord channel via webhook: player name, points, time, kills/treasures/secrets, and a gold "New #1" callout when someone takes the top spot.

Configure in `.env` (or `Discord:*` keys in `data/appsettings.json`):

```bash
# Server Settings > Integrations > Webhooks > Copy Webhook URL
DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/...

# Optional: appended as a "Play now" link on every announcement
DISCORD_GAME_URL=https://wolf3d.m0b.tech

# Optional: only announce top-N placements per level (0 = announce everything)
DISCORD_ANNOUNCE_TOP_RANKS=0
```

Restart after changing `.env`:

```bash
docker compose up -d
```

Announcements are fire-and-forget: a failing or unset webhook never blocks score submission. Leave `DISCORD_WEBHOOK_URL` empty to disable the feature.

## Timezone

Score `SubmittedAt` timestamps use the container's OS timezone. Set `TZ` in `.env` to an [IANA timezone name](https://en.wikipedia.org/wiki/List_of_tz_database_time_zones) before starting the stack:

```bash
TZ=Europe/Amsterdam
```

Rebuild or restart after changing `TZ`:

```bash
docker compose up -d --build
```

No application config is required; the image ships `tzdata` and passes `TZ` through to the runtime.

## Cloudflare Tunnel

Cloudflare Tunnel exposes the API without opening inbound ports on your host. TLS terminates at Cloudflare; `cloudflared` forwards HTTP to the `highscores` container on the internal Docker network.

### 1. Create a tunnel

1. Open [Cloudflare Zero Trust](https://one.dash.cloudflare.com/) > **Networks** > **Tunnels**.
2. **Create a tunnel** > choose **Cloudflared**.
3. Name the tunnel (for example `wolfrender-highscores`).
4. Copy the **tunnel token**.

### 2. Configure `.env`

```bash
TUNNEL_TOKEN=eyJhIjoi...
HIGHSCORES_PORT=5080
```

### 3. Start server + tunnel

```bash
docker compose --profile tunnel up -d --build
```

### 4. Add a public hostname

In the tunnel's **Public Hostname** settings:

| Field | Value |
|-------|-------|
| Subdomain | e.g. `wolf3d` |
| Domain | your Cloudflare zone |
| Type | HTTP |
| URL | `highscores:5080` |

Use the Docker Compose service name (`highscores`), not `localhost`. Both containers share the `wolfrender-highscores` network.

### Docker networking

`cloudflared` and `highscores` are attached to the same user-defined network (`wolfrender-highscores`). They can reach each other without knowing IP addresses:

| From | To | URL |
|------|----|-----|
| `cloudflared` | `highscores` | `http://highscores:5080` |
| host (published port) | `highscores` | `http://localhost:5080` |

Docker Compose registers each service name in an internal DNS server. **Use the service name in the Cloudflare tunnel config** (`highscores:5080`). Container IPs are assigned dynamically and can change when containers are recreated.

To inspect current IPs (optional):

```bash
docker network inspect wolfrender-highscores \
  --format '{{range .Containers}}{{.Name}} {{.IPv4Address}}{{"\n"}}{{end}}'
```

Example output:

```text
wolfrender-highscores 172.27.0.2/16
wolfrender-cloudflared 172.27.0.3/16
```

Test reachability from another container on the same network:

```bash
docker run --rm --network wolfrender-highscores curlimages/curl:latest \
  http://highscores:5080/api/scores/test-level
```

The API listens on all interfaces inside the container (`0.0.0.0:5080` via `ASPNETCORE_URLS`), so other containers on the network can connect to port 5080.

### 5. CORS

Add your public game URL to `Cors:Origins` in `data/appsettings.json`, then restart:

```bash
docker compose restart highscores
```

The server reloads `/data/appsettings.json` when the file changes.

## Operations

```bash
# Logs
docker compose logs -f highscores
docker compose logs -f cloudflared

# Stop
docker compose --profile tunnel down

# Rebuild after code changes
docker compose --profile tunnel up -d --build
```

## Notes

- The app listens on HTTP inside the container. Cloudflare handles HTTPS for public clients.
- Forwarded headers (`X-Forwarded-Proto`) are enabled so HTTPS redirection behaves correctly behind the tunnel.
- Keep `data/highscores.json` backed up; it is gitignored by default.
