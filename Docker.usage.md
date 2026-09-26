# Running this server in Docker

Files: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, and for remote admin access
`docker-compose.admin-remote.yml` + `Caddyfile`.

```bash
cp .env.example .env          # then edit MEDIA_PATH, PUID, PGID
docker compose up --build -d
docker compose logs -f
```

Admin pages: `http://<host>:26853` - the admin port's root redirects to `/admin`. Televisions find the
server on their own.

---

## The four things that will bite you

### 1. Host networking is not optional

DLNA discovery is SSDP: UDP multicast to `239.255.255.250:1900`. **Docker's bridge network does not
forward multicast**, in either direction. On a bridge:

- the admin pages work perfectly,
- the media port answers every request you make by hand,
- and **no television ever lists the server**.

That looks exactly like a bug in the server and is not one. `docker-compose.yml` uses
`network_mode: host` for this reason, which also means `ports:` is ignored — the ports come from
`Dlna.Server.Port` and `Dlna.Server.AdminPort`, and they are already on the host.

Observed on a bridge during testing: the server logged `Advertising on 172.17.0.2`. Even if a television
received that announcement, every media and thumbnail URL in it points at a Docker-internal address the
television cannot route to. Host networking is what makes it advertise a real LAN address.

Bridge networking is only reasonable if nothing needs to discover the server — which in practice means
you are using the admin UI and nothing else.

**On Docker Desktop for Windows or Mac, `network_mode: host` is not the same thing** — the "host" is the
Linux VM, not your workstation. This compose file is for a Linux host: the NAS, or a Linux box. Testing
on Windows works with published ports, but discovery will not.

### 2. Why the media mount is not read-only

Previews are written **beside the media**, into the folder named by `Thumbnails.SubFolderName`
(`.@__thumb`). That is deliberate: an existing library arrives already previewed, and a redeploy does not
throw them away. `Thumbnails.CacheDirectory` exists but its branch is unreachable, so there is no
supported way to put previews somewhere else today.

So the media mount is read-write, and **the container user must own or be able to write to the media
tree**. Set `PUID`/`PGID` in `.env` to the owner of your media (`id -u`, `id -g` on the host). Mount
`:ro` only if you accept a library with no previews at all.

### 3. Settings changed in the UI are lost unless you mount `config.json`

The Settings page writes back to `config.json`, which lives next to the binaries — inside the image. Any
setting you change in the UI disappears the next time the container is recreated.

Once past the first run, uncomment the `./config.json:/app/config.json` mount in `docker-compose.yml`:

```bash
docker compose cp dlna:/app/config.json ./config.json
# uncomment the mount, then
docker compose up -d
```

A mounted `config.json` overrides every `Dlna__` environment variable, so remove the ones you have moved
into the file rather than leaving both.

### 4. Turn the periodic rescan off if you do not need it

`Dlna__Library__UsePeriodicRescan` ships **on** in `docker-compose.yml`, and that is the only place it
defaults to on. The reason is that a bind mount or an overlay filesystem usually delivers no inotify
events for writes made outside the container, so `FileSystemWatcher` stays silent and the library quietly
stops keeping up.

It is not free. Each pass enumerates and reconciles every source folder — roughly 51,000 filesystem calls
on a 25,000-file library. If your media is on a real local filesystem and the watcher works, set it to
`false`. If you keep it, raise `RescanIntervalMinutes` rather than lowering it. The Maintenance page has
a *Look for new, changed and removed files* button for the times you just want one pass now.

A real local filesystem with a very large directory tree can hit a different limit instead: one inotify
watch per directory, capped host-wide by `fs.inotify.max_user_watches`. See
[Troubleshooting](docs/troubleshooting.md) for the symptom and the fix - it is a host `sysctl`, not a
container setting.

---

## ARM64

**Nothing to change.** The publish carries no runtime identifier, so it emits IL plus a `runtimes/`
folder holding every architecture's native assets, and the loader picks at startup. Both native
dependencies ship ARM: `SkiaSharp.NativeAssets.Linux.NoDependencies` and `SQLitePCLRaw.lib.e_sqlite3`
each include `linux-arm64` and `linux-musl-arm64`.

```bash
docker buildx build --platform linux/arm64 -t dlna-server:local --load .
```

The SDK stage is pinned to `--platform=$BUILDPLATFORM`, so cross-building from an x64 workstation runs
the compiler natively and only the small runtime stage is emulated. Without that the whole SDK runs
under QEMU and a 90-second build becomes tens of minutes.

Verified on 2026-09-06 by building for `linux/arm64` and running the result under emulation: image
reports `Arch=arm64`, `runtimes/linux-arm64/native/` carries `libSkiaSharp.so` and `libe_sqlite3.so`,
all six migrations applied, **SkiaSharp generated a real 480×360 thumbnail**, `ffprobe 5.1.9` ran, and
the admin UI answered 200. SOAP Browse was not exercised on ARM — a harness mistake, not a failure — but
it is pure managed code with no native dependency of its own.

`ReadyToRun` is off, which helps here: R2R needs a runtime identifier and the matching runtime pack, and
cross-compiling it to ARM from an x64 builder is the awkward case. It only shortens JIT warm-up.

## What the image does differently from `NasBuild.sh`

| | NAS | Docker |
|---|---|---|
| ffmpeg | absent unless `DownloadFFmpeg` is turned on | **installed from Debian**, `DownloadFFmpeg` stays off |
| Database | `dlna.sqlite` beside the binaries | `/data/dlna.sqlite`, its own volume |
| Logs | `logs/` beside the binaries | `/app/logs`, its own volume |
| `config.json` | preserved across deploys by the script | inside the image unless you mount one |
| GC / allocator | exported in the generated `run.sh` | baked into the image as `ENV` |

The ffmpeg difference is a real improvement, not just packaging. `Thumbnails.DownloadFFmpeg` fetches an
archive from whatever URL a third-party API names, verifies no hash or signature, and executes the result
as the server user — so a compromise of that host, its CDN or its DNS is code execution on your box.
Debian's package is signed, and `docker build --pull` updates it.

## Memory

The allocator and GC settings from `NasBuild.sh` are baked in: `DOTNET_GCServer=0`,
`MALLOC_TRIM_THRESHOLD_`, `MALLOC_MMAP_THRESHOLD_`, `MALLOC_ARENA_MAX=2`.

The settings that actually decide the footprint are in `docker-compose.yml`, and the important one is
`Dlna__Database__CacheSizeInMegabytes`. **It is charged per pooled connection, not per process.** At the
value the NAS was accidentally running (32), a pool grown to a dozen connections is ~380 MB of native
memory that nothing reclaims — the pool releases nothing when a connection is returned and prunes only
after two 120-240 second ticks. Leave it at 2.

`mem_limit` defaults to 1g. Below about 512m, thumbnailing a large photo fails rather than merely being
slow: SkiaSharp decodes up to 24 million pixels at 4 bytes each.

When memory does climb, *Empty the memory* on `/admin/cache` clears the byte cache, releases the pooled
connections' page caches, collects and compacts the managed heap, and calls `malloc_trim` — the last of
which is the only thing that returns glibc arena memory to the operating system. It reports working set
either side so you can see what each part gave back.

---

## Exposing only the admin pages to the internet

```bash
docker compose -f docker-compose.yml -f docker-compose.admin-remote.yml up -d
```

**Consider a VPN first.** WireGuard on the router or the NAS gives you the admin pages, the media port and
everything else with no public surface. The setup below exists for when that genuinely is not an option.

**What you are exposing.** The admin pages can restart the server, stop it, delete the index and rewrite
`config.json`. The server has **no authentication of any kind** — a deliberate trusted-LAN decision
recorded in `ManageController`'s own remarks. Everything protecting it is the proxy.

**What you are not exposing, and why that is load-bearing.** `/manage` — including `POST /manage/stop` —
lives on the **media** port, and `AdminSurfaceMiddleware` enforces the split for the Blazor paths too.
Verified against the running server: `/manage/database` answers 200 on 26852 and 404 on 26853. So
publishing the admin port does not publish the management API.

> **"Never publish the media port" used to follow, and it described a control that does not exist.**
> `docker-compose.yml` mandates `network_mode: host` because SSDP needs multicast, and `Program.cs` binds
> both ports with `ListenAnyIP` — the media port is already on every interface, so there is nothing to
> publish or withhold and the **host firewall is the only thing confining it**.
>
> That mattered because `ADMIN_HOSTNAME` widens `AllowedHosts` for the whole **process** —
> `HostFilteringOptions` has no per-port form — so setting it for the admin pages also let
> internet-origin requests past the one in-process check that had been refusing them on the media port.
> The hostname is not a secret: the Let's Encrypt certificate for it is published in the Certificate
> Transparency logs, so anyone can find it. A single `curl` with a spoofed `Host` header reached
> `POST /manage/stop`, entirely past Caddy's password.
>
> `RejectRemoteManagementEndpointFilter` now refuses `/manage` from any non-private remote address, which
> is the real control. **Keep the firewall anyway** — that filter protects `/manage`, not the media
> streaming endpoints, and this server has no authentication by design.
>
> **The same process-wide reach cuts the other way too, and it costs LAN traffic rather than security.**
> Naming `ADMIN_HOSTNAME` in `AllowedHosts` is `AllowedHostsDefaults`'s own documented escape hatch —
> `Apply()` returns early the moment the list is non-wildcard — so its LAN-address auto-detection never
> runs at all once the overlay is up. A television that reaches this box by its LAN IP or mDNS name
> (`description.xml`, SOAP, streaming — one process, one `AllowedHosts`) gets the identical 400 the
> hostname guard was built to give a stranger. `LAN_HOSTS` in `.env` is the fix: name the LAN address(es)
> explicitly rather than falling back to `*`, which would silently drop the DNS-rebinding guard back to
> nothing.

### Setup

1. Point a DNS name at your address and forward **443** (and **80**, for the certificate challenge) to
   the host. Forward nothing else.
2. Generate a password hash, at cost 10-12 rather than the tool's default 14 — `basic_auth` has no
   brute-force limiting of its own, and cost 14 is roughly a second of NAS CPU per guess, which is an
   unauthenticated lever an attacker can pull from the internet. Cost 10-12 still costs real time per
   guess without making a real login sluggish. Run `fail2ban` against `admin-access.log` (below) if this
   proxy faces the open internet and that residual guess rate still worries you:
   ```bash
   docker run --rm caddy:2-alpine caddy hash-password --cost 12 --plaintext 'your-password'
   ```
3. Put it in `.env`, doubling every `$` so compose does not read it as a variable:
   ```
   ADMIN_HOSTNAME=dlna.example.com
   ADMIN_USER=admin
   ADMIN_PASSWORD_HASH=$$2a$$14$$....
   ```
4. Also set `LAN_HOSTS` to this box's LAN IP and/or hostname, semicolon-separated, unless nothing on the
   LAN ever reaches it by address — see the callout above for why `ADMIN_HOSTNAME` alone breaks that.

### The two failures you will hit if you improvise this

- **A bare 400 from the server, no page, no log entry you would recognise.** `AllowedHostsDefaults`
  replaces the shipped wildcard with loopback, the machine name and its LAN addresses — a DNS-rebinding
  guard, because nothing else separates a hostile page from an unauthenticated same-origin admin UI. A
  request arriving as `dlna.example.com` matches none of them. The overlay sets `AllowedHosts`
  explicitly, which is the documented escape hatch; `Apply()` returns early on a non-wildcard list.
- **Every page renders once, looks right, then does nothing when clicked.** The Blazor circuit is a
  WebSocket. A proxy that does not forward `Upgrade` leaves the UI dead but visually intact. Caddy's
  `reverse_proxy` handles it; nginx needs `proxy_set_header Upgrade`/`Connection` explicitly.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| No television lists the server | Bridge networking. SSDP is multicast; use `network_mode: host` |
| Library is empty | `Dlna__Library__SourceFolders__0` does not point at the mount, or nothing under it matches `Library.MediaFileExtensions` |
| No previews, no errors | The container user cannot write `.@__thumb` into the media tree — check `PUID`/`PGID` |
| No durations, resolutions or language filters | ffmpeg missing. `docker compose exec dlna /app/ffmpeg/ffprobe -version` |
| New files never appear | The watcher gets no inotify events. Set `UsePeriodicRescan=true` |
| Settings revert after a rebuild | `config.json` is not mounted |
| Admin page returns 400 remotely | `AllowedHosts` does not include the external name |
| A renderer on the LAN gets 400 with `docker-compose.admin-remote.yml` up | `LAN_HOSTS` is unset - `ADMIN_HOSTNAME` alone disables the automatic LAN-address detection for the whole process |
| Working set climbs and stays | Press *Empty the memory* on `/admin/cache`; check `CacheSizeInMegabytes` is 2, not 32 |

Useful:

```bash
docker compose exec dlna curl -s localhost:26852/manage/memory
docker compose exec dlna curl -s localhost:26852/manage/database
docker compose exec dlna curl -s localhost:26852/manage/configuration
```

## What was actually verified, and what was not

Built and run on 2026-09-06, Docker 28.3.3. Image is 1.25 GB.

**Verified by running it**, as uid 1000 with a bind-mounted media folder:

| | Result |
|---|---|
| `docker build` | succeeds |
| `docker compose config`, both files and the overlay merged | valid; `AllowedHosts` reaches the server |
| Container health | `Up (healthy)` — the `HEALTHCHECK` works |
| Migrations on an empty volume | all six applied, database created |
| Indexing | 2 files, 2 directories from the mount |
| **Thumbnail generation** | 480×360, 4045 bytes, written to `/media/Films/.@__thumb/big.jpg.jpg` **as uid 1000**, visible on the host — so the non-root write into the media tree works |
| `hasStoredContent` | true, so the database copy is written too |
| ffmpeg | `ffprobe 5.1.9` resolves at `/app/ffmpeg/ffprobe` with `DownloadFFmpeg` off |
| Periodic rescan | fires on the interval |
| Admin UI | 200, and the *Advanced* group renders collapsed |
| **Port confinement** | `/manage/database` → **404 on the admin port**, 200 on the media port |

**Two bugs in the first version of this Dockerfile, both found by running it and both fixed:**

1. `/data` and `/app/logs` were created as root while the container runs non-root. SQLite reported
   `unable to open database file`, the initialiser treated it as fatal, and the container **exited 139
   before serving anything**. They are `chmod 0777` now, because the operator picks the uid.
2. Data-protection keys went to `$HOME/.aspnet` inside the container and were lost on every recreate.
   `HOME=/data` now persists them on the volume.

**`/data` is worth treating as sensitive, not just as "the database".** The Blazor data-protection key
ring lives there too, unencrypted on disc, and it is what lets a stored antiforgery token or cookie stay
valid across a container recreate — copy or expose the volume and whoever holds it can forge either
against a running instance. Back it up like the database, but do not hand it out like the media mount.

**Not verified, and the reason:**

- **SSDP discovery by a real television.** That needs a renderer on the LAN, which this machine is not.
  The bridge-mode claim is not guesswork though: the container logged
  `Advertising on 172.17.0.2` — a Docker-internal address. Even if a television received that
  announcement, every media and thumbnail URL in it points somewhere the television cannot route to.
  Host networking is what makes it advertise a real LAN address.
- **`network_mode: host` on Docker Desktop for Windows or Mac.** It is a Linux feature; on Desktop the
  "host" is the Linux VM, not your workstation, so the compose file as written is for a Linux host — the
  NAS, or a Linux box. On Windows, test with published ports and accept that discovery will not work.
- **The remote-admin overlay end to end.** Caddy's config is valid and the merge is correct, but no
  certificate was issued and no external request was made, because that needs a public DNS name.
