# 2 - admin pages reachable from the internet

The DLNA server plus a [Caddy](https://caddyserver.com/) proxy that puts **only** the admin pages on the
internet, behind TLS and a password. A standalone stack: the `dlna` service is complete here, so this folder
does not need folder 1.

**A VPN is the better answer.** WireGuard on the router or the NAS gives you the admin pages and everything
else with no public surface at all. The server itself has no authentication by design, so the proxy is the
whole of the protection. Read the warning at the top of `docker-compose.yml` before going further.

| File | What it is |
| --- | --- |
| `docker-compose.yml` | `dlna` (built from the GitHub repository) and `caddy`, both on host networking |
| `Caddyfile` | TLS, basic auth, WebSocket upgrade for the Blazor circuit, and refusal of any other host name |
| `.env.example` | Every value the stack reads; the admin-proxy section is required |

## Setup

1. Point a DNS name at the host and open ports 80 and 443 to it, for the Let's Encrypt challenge. Keep the
   media port **closed** at the firewall: host networking puts it on every interface.
2. Create the password hash. Use cost 10-12, not the default 14; the reason is in `.env.example`:
   ```bash
   docker run --rm -it caddy:2-alpine caddy hash-password --cost 12
   ```
3. Fill in `.env`:
   ```bash
   cp .env.example .env
   ```
   Set `MEDIA_PATH`, `PUID`/`PGID`, `ADMIN_HOSTNAME`, `ADMIN_USER` and `ADMIN_PASSWORD_HASH`. Double every
   `$` in the hash. Set `LAN_HOSTS` to the host's LAN address and name, or televisions get a 400.
4. Start it:
   ```bash
   docker compose up --build -d
   ```

`ADMIN_PORT` drives both the server's admin port and Caddy's upstream, so change it in one place only.

[`Docker.usage.md`](../../../Docker.usage.md) has the full background under *Exposing only the admin pages
to the internet*, including the two failures you hit by improvising this.
