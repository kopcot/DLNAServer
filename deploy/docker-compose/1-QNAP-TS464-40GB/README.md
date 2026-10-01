# 1 - QNAP TS-464, 40 GB

The server on the home NAS: a QNAP TS-464 with 40 GB of RAM.

| | |
| --- | --- |
| Media | `/share/Media` on the NAS, bind-mounted read-write at `/media` |
| Media port | `25002` - televisions find it on their own |
| Admin pages | `http://<nas>:25003` |
| Database, logs | named volumes `dlna_database` and `dlna_logs` |
| Networking | `network_mode: host` - SSDP discovery needs it |

## Run

From this folder:

```bash
cp .env.example .env          # set PUID/PGID to the owner of /share/Media
docker compose up --build -d
docker compose logs -f
```

The build context is `https://github.com/kopcot/DLNAServer.git#main`, so the image is built from what is on
GitHub, not from the checkout this file sits in. To update, rebuild:

```bash
docker compose build --pull && docker compose up -d
```

To run a released image instead, replace the `build:` block with `image: ghcr.io/kopcot/dlnaserver:latest`
and use `docker compose pull && docker compose up -d`.

## Before changing anything

[`Docker.usage.md`](../../../Docker.usage.md) explains the four things that bite: host networking, why the
media mount is read-write, where `config.json` lives, and the periodic rescan this file turns on. The ports
and memory settings come from the `environment:` block, and those values win over the Settings page.

To put only the admin pages on the internet, see [`2-admin-remote`](../2-admin-remote/README.md).
