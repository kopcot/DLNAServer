# syntax=docker/dockerfile:1

# DLNA media server - see Docker.usage.md before running this.
#
# THE ONE THING TO KNOW: DLNA discovery is SSDP, which is UDP multicast on 239.255.255.250:1900.
# Docker's bridge network does not forward multicast, so a container on a bridge is INVISIBLE to every
# television on the LAN however many ports are published. The compose file uses host networking for
# that reason. Publishing ports instead is only useful when nothing needs to discover the server.

# ---------------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------------
# global.json pins 8.0.414 with rollForward latestPatch, so the 8.0 SDK tag resolves an acceptable
# 8.0.4xx. Pinned to a digest-free but explicit tag rather than 'latest' for the same reason global.json
# exists: the .NET 9/10 SDKs would be selected otherwise and this solution targets net8.0.
#
# --platform=$BUILDPLATFORM pins the SDK stage to the machine doing the building. Without it, a
# `buildx --platform linux/arm64` from an x64 workstation runs the whole SDK under QEMU emulation, which
# turns a 90-second build into tens of minutes. The publish below is architecture-neutral, so building
# on x64 for arm64 is not merely allowed here - it is the fast path.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first, so a source-only change does not re-run restore. Every csproj is copied because
# Directory.Packages.props uses central package management and restore reads the whole graph.
COPY global.json Directory.Build.props Directory.Packages.props DlnaServer.sln ./
COPY src/DlnaServer.Host/config.json                            app/
COPY src/DlnaServer.Core/DlnaServer.Core.csproj                 src/DlnaServer.Core/
COPY src/DlnaServer.Persistence/DlnaServer.Persistence.csproj   src/DlnaServer.Persistence/
COPY src/DlnaServer.Media/DlnaServer.Media.csproj               src/DlnaServer.Media/
COPY src/DlnaServer.Upnp/DlnaServer.Upnp.csproj                 src/DlnaServer.Upnp/
COPY src/DlnaServer.Admin/DlnaServer.Admin.csproj               src/DlnaServer.Admin/
COPY src/DlnaServer.Host/DlnaServer.Host.csproj                 src/DlnaServer.Host/
COPY tests/DlnaServer.UnitTests/DlnaServer.UnitTests.csproj                 tests/DlnaServer.UnitTests/
COPY tests/DlnaServer.IntegrationTests/DlnaServer.IntegrationTests.csproj   tests/DlnaServer.IntegrationTests/
COPY tests/DlnaServer.ArchitectureTests/DlnaServer.ArchitectureTests.csproj tests/DlnaServer.ArchitectureTests/

RUN dotnet restore DlnaServer.sln

COPY . .

# Framework-dependent, matching NasBuild.sh: the runtime image already carries the shared framework, and
# a self-contained publish would add ~70 MB of duplicate runtime to every layer.
#
# NO RUNTIME IDENTIFIER, deliberately, and this is what makes one build serve x64 and arm64 alike. A
# portable publish emits IL plus a `runtimes/` folder carrying every architecture's native assets, and
# the loader picks the right one at startup. Both native dependencies ship arm64 - verified in the
# packages: SkiaSharp.NativeAssets.Linux.NoDependencies and SQLitePCLRaw.lib.e_sqlite3 both include
# linux-arm64 and linux-musl-arm64. Pinning a RID here would instead force a separate build per
# architecture and re-introduce the restore-graph trap NasBuild.sh documents at length.
#
# ReadyToRun is deliberately absent. It only shortens JIT warm-up, it needs a RID and the matching
# runtime pack, and crossgen2 already crashes on the NAS - NasBuild.sh ships with it off for that reason.
# An earlier version of this file passed `-p:ReadyToRun=true`, which is not an MSBuild property at all:
# `dotnet msbuild -getProperty:PublishReadyToRun -p:ReadyToRun=true` returns empty, so it had never done
# anything. The real name is PublishReadyToRun.
RUN dotnet publish src/DlnaServer.Host/DlnaServer.Host.csproj \
        --configuration Release \
        --no-restore \
        --output /publish

# ---------------------------------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# ffmpeg from the distribution, deliberately, so Thumbnails.DownloadFFmpeg can stay OFF. That setting
# fetches an archive from whatever URL a third-party API names, verifies no hash or signature, and then
# executes the result as the server user - a compromise of that host, its CDN or its DNS would be code
# execution here. Debian's package is signed and updated by 'docker build --pull'.
#
# curl is for the HEALTHCHECK below and for reaching /manage from inside the container.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ffmpeg curl \
    && rm --recursive --force /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /publish ./
COPY --from=build /app/config.json ./config.json

# FFmpegProvisioner looks for an 'ffmpeg' folder beside the binaries and uses whatever is in it without
# downloading. Symlinks rather than copies so an apt upgrade of ffmpeg is picked up.
RUN mkdir --parents /app/ffmpeg \
    && ln --symbolic /usr/bin/ffmpeg  /app/ffmpeg/ffmpeg \
    && ln --symbolic /usr/bin/ffprobe /app/ffmpeg/ffprobe

# The database and the logs must outlive the container, and both default to paths under the content
# root, which is the image. Redirected here and mounted as volumes in compose.
#
# 0777, deliberately. The operator picks the container's uid to match whoever owns their media - that is
# what PUID/PGID in compose are for - so these two directories cannot be chowned to a uid chosen at build
# time. Getting it wrong is not a warning: SQLite reports `unable to open database file`, the database
# initialiser treats that as fatal, and the container exits 139 before serving anything. Both are
# container-private volumes holding a derived index and its logs, so a permissive mode costs nothing that
# the media mount is not already exposing.
RUN mkdir --parents /data /app/logs \
    && chmod 0777 /data /app/logs

ENV ConnectionStrings__DlnaDatabase="Data Source=/data/dlna.sqlite;Pooling=True;Default Timeout=30;" \
    #
    # Blazor Server's circuits use data protection, whose keys default to $HOME/.aspnet and are therefore
    # lost with the container - the framework warns about exactly this at startup. Pointing HOME at the
    # data volume persists them, so the admin UI survives a `docker compose up -d` without every open
    # page's circuit being invalidated.
    HOME=/data \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    #
    # Workstation GC, as on the NAS: Server GC reserves a heap and a thread per core, and on a small box
    # aiming at a ~60 MB managed heap that stopped the collector running often enough to matter.
    DOTNET_GCServer=0 \
    #
    # SkiaSharp decode buffers are large and short-lived; without a lowered trim threshold glibc keeps
    # them. ARENA_MAX=2 caps the number of arenas, which is what makes a trim likely to return anything.
    # NOTE: SQLite's page cache is allocated in ~4 KiB chunks, BELOW MMAP_THRESHOLD_, so it comes from an
    # arena and is never returned on its own - that is what NativeHeapTrimmer.TryTrim is for, reachable
    # from 'Empty the memory' on the admin pages.
    MALLOC_TRIM_THRESHOLD_=65536 \
    MALLOC_MMAP_THRESHOLD_=65536 \
    MALLOC_ARENA_MAX=2

# Both ports are declared for documentation. Under host networking they are not published and the real
# values are whatever Dlna.Server.Port and Dlna.Server.AdminPort say.
EXPOSE 26852 26853

# The media port answers /manage without authentication and is the surface a television uses, so it is
# the honest liveness probe. start-period covers the first library scan on a large volume.
HEALTHCHECK --interval=60s --timeout=10s --start-period=120s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 8 \
        "http://127.0.0.1:${Dlna__Server__Port:-26852}/manage/database" || exit 1

# Non-root. The container must still be able to WRITE INTO THE MEDIA TREE, because thumbnails are stored
# beside the media in Thumbnails.SubFolderName - see Docker.usage.md, "Why the media mount is not
# read-only". Override with `user:` in compose to match the owner of your media.
USER $APP_UID

ENTRYPOINT ["dotnet", "DlnaServer.Host.dll"]
