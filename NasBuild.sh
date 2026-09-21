#!/bin/bash
#
# Build and publish DlnaServer.Host for the QNAP NAS (framework-dependent, architecture detected).
# The NAS has the .NET 8.0 runtime installed, so a self-contained publish is not needed.
#
# See NasBuild.usage.txt for full usage.

set -euo pipefail

# pwd -P, not pwd. On this NAS /share/Public is a symlink to /share/CACHEDEV1_DATA/Public, and `pwd`
# keeps the symlinked spelling. The Host would then be restored as
#   /share/Public/repos/.../DlnaServer.Host.csproj
# while MSBuild resolved its relative ProjectReferences to
#   /share/CACHEDEV1_DATA/Public/repos/.../DlnaServer.Core.csproj
# - the same files under two names. NuGet built the Host's graph across both spellings and dropped the
# project references entirely: project.assets.json came out with 34 entries, no "type": "project" libraries
# and none of the packages those projects bring. A correct restore of the same tree records 4 project
# references and 57 entries. The build still succeeded, because MSBuild resolves ProjectReferences without
# NuGet - so the failure only appeared at runtime, as a missing EntityFrameworkCore.Relational.
# Resolving to the physical path keeps every project in one path universe.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$SCRIPT_DIR/src/DlnaServer.Host/DlnaServer.Host.csproj"

PUBLISH_DIR="${1:-/share/Internal/DLNA/publish2}"
ASSEMBLY_NAME="${2:-DlnaServer}"
RUN_AFTER_PUBLISH="${3:-no}"

# ReadyToRun is OFF by default because crossgen2 crashes on this NAS. Its SDK 8.0.414 fails to load the
# native JIT the compiler needs:
#
#   at System.Runtime.InteropServices.NativeLibrary.Load(String, Assembly, Nullable`1)
#   at Internal.JitInterface.CorInfoImpl.Startup(CORINFO_OS)
#   error NETSDK1096: Optimizing assemblies for performance failed.
#
# R2R only pre-compiles IL to shorten JIT warm-up, so losing it costs a slower first few requests on a
# process that then runs for weeks - not worth a build that cannot complete. Set NAS_READY_TO_RUN=1 to
# try it again after an SDK update on the NAS.
# The NAS's own architecture, because this script runs ON the NAS. Publishing linux-x64 onto an ARM
# box produces a folder that looks complete and dies at the first instruction, so this is detected
# rather than assumed. NAS_RUNTIME overrides it - set NAS_RUNTIME=linux-musl-arm64 on a musl system,
# which is not what QNAP runs but is what an Alpine container would need.
#
# No package changes are needed for ARM: SkiaSharp.NativeAssets.Linux.NoDependencies and
# SQLitePCLRaw.lib.e_sqlite3 both ship linux-arm64 and linux-musl-arm64 natives.
case "$(uname -m)" in
  x86_64 | amd64)   DETECTED_RUNTIME="linux-x64" ;;
  aarch64 | arm64)  DETECTED_RUNTIME="linux-arm64" ;;
  armv7l | armv8l)  DETECTED_RUNTIME="linux-arm" ;;
  *)
    echo "ERROR: unrecognised machine type '$(uname -m)'." >&2
    echo "       Set NAS_RUNTIME explicitly, e.g. NAS_RUNTIME=linux-arm64 ./NasBuild.sh" >&2
    exit 1
    ;;
esac

RUNTIME="${NAS_RUNTIME:-$DETECTED_RUNTIME}"

READY_TO_RUN="${NAS_READY_TO_RUN:-0}"

if [ "$READY_TO_RUN" = "1" ]; then
  PUBLISH_R2R=true
else
  PUBLISH_R2R=false
fi

if [ ! -f "$PROJECT" ]; then
  echo "ERROR: project not found at $PROJECT" >&2
  exit 1
fi

echo "==> Project      : $PROJECT"
echo "==> Publish dir  : $PUBLISH_DIR"
echo "==> Assembly     : $ASSEMBLY_NAME"
echo "==> Run after    : $RUN_AFTER_PUBLISH"
echo "==> Runtime      : $RUNTIME"
echo "==> ReadyToRun   : $PUBLISH_R2R"

# =========================
# ENVIRONMENT
# =========================
# MALLOC_* keep native allocator growth in check - SkiaSharp thumbnail generation grows RSS
# without bound on glibc unless the trim thresholds are lowered.
#
# MALLOC_ARENA_MAX=2, not 32: glibc's own default is 8 x ncores, and the NAS reports 4 cores, so 32 set
# it to exactly what it would have done anyway. Each arena retains up to 64 MB of freed-but-untrimmed
# heap - ~2 GB of authorised native fragmentation, which is the bucket the reference's unexplained
# ~350 MB of working set sits in.
#
# DOTNET_GCServer=0 for the reason in Directory.Build.props: four heaps and four GC threads on a 4-core
# box, for a 60 MB managed-heap target, is why the GC had essentially stopped running.
export DOTNET_GCServer=0
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export MALLOC_TRIM_THRESHOLD_=65536
export MALLOC_MMAP_THRESHOLD_=65536
export MALLOC_ARENA_MAX=2

mkdir -p "$PUBLISH_DIR"

# Same reason as SCRIPT_DIR above: keep the output path in the same path universe as the projects.
PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd -P)"

# =========================
# CLEAN PREVIOUS OUTPUT
# =========================
# Scoped to build artifacts only. The database, logs, thumbnail cache and config.json
# live alongside the binaries and must survive a redeploy.
echo "==> Cleaning previous build artifacts (config.json, logs, database and thumbnails are preserved)"
#
# -maxdepth 1 matters: Resources/ lives one level down and is republished wholesale, while config.json,
# logs/, the database and the thumbnail cache must survive. The image and xml patterns clear two kinds of
# build output - the generated XML documentation files, and the loose icons and SCPD documents left in
# the root by the period when Resources was published flattened. No user data has those extensions.
find "$PUBLISH_DIR" -maxdepth 1 -type f \( \
  -name "*.dll" -o \
  -name "*.pdb" -o \
  -name "*.deps.json" -o \
  -name "*.runtimeconfig.json" -o \
  -name "*.so" -o \
  -name "*.a" -o \
  -name "*.xml" -o \
  -name "*.jpg" -o \
  -name "*.png" \
\) -delete

# =========================
# RESTORE / BUILD / PUBLISH
# =========================
# PublishAssemblyName goes to all three steps on purpose.
#
# It must NOT be passed as -p:AssemblyName. That is a global MSBuild property, so it renames every
# project in the graph: Core, Persistence, Media and Upnp all become the same assembly, the compiler
# sees four references sharing one identity, and the Host fails to compile against types that have
# silently vanished. On newer SDKs NuGet refuses the restore outright with "Ambiguous project name".
# DlnaServer.Host.csproj reads PublishAssemblyName and renames only itself.
#
# Passing it to build as well as publish keeps the two steps on the same global properties. Otherwise
# publish recompiles from scratch and can fail on code the build step just reported as green - which is
# how this defect stayed hidden until a real deployment hit it.
# PublishReadyToRun must be set at RESTORE time too, not only at publish: R2R compilation needs the
# target runtime pack, and restore only fetches it when it knows R2R is coming. Without it publish
# fails with NETSDK1094 "a valid runtime package was not found".
#
# --force is not paranoia. Without it NuGet reports "4 of 5 projects are up-to-date for restore" and
# reuses whatever obj/ state is already there. A deployment left holding stale state from an earlier,
# broken run published a deps.json listing only the Host's own packages - every package arriving through
# a referenced project (EF Core, SkiaSharp, SQLitePCLRaw, Xabe.FFmpeg, CommunityToolkit) was absent from
# both the graph and the output, and the server died at startup on
# "Could not load file or assembly 'Microsoft.EntityFrameworkCore.Relational'".
# Recomputing the whole graph every deployment costs a few seconds and removes that entire failure mode.

# The architecture and golden-file tests are the only mechanical proof that a change has not broken the
# layer boundaries or the bytes a television parses, and this script is the only path to the NAS - there
# is no git here and no CI, so without this all 410 tests are a discipline rather than a gate and a publish
# can skip every one of them. ArchitectureTests and the wire golden files need no fixture, no NAS and
# no I/O, so they cost seconds; SKIP_TESTS=1 exists for a deliberate hurry, not for routine use.
#
# BEFORE the restore, and that ordering is load-bearing. `dotnet test` on the solution runs its own
# implicit restore with NO --runtime, and DlnaServer.IntegrationTests references the Host - so running
# it after the restore below rewrote src/DlnaServer.Host/obj/project.assets.json with a RID-less
# target, and the publish then died on NETSDK1047 "doesn't have a target for net8.0/<runtime>".
# Running it first means the --force restore below is the last thing to touch that file, and the guard
# after it still validates what publish will actually consume. Failing here also costs less: the
# restore and the build have not run yet.
if [ "${SKIP_TESTS:-0}" = "1" ]; then
  echo "==> Skipping tests (SKIP_TESTS=1)"
else
  echo "==> Testing (architecture + wire contract)"
  dotnet test "$SCRIPT_DIR/DlnaServer.sln" \
    --configuration Release \
    --filter "FullyQualifiedName~ArchitectureTests|FullyQualifiedName~WireGoldenFileTest|FullyQualifiedName~DidlSerializerTest|FullyQualifiedName~SoapContractWireNameTest" \
    --nologo \
    --verbosity quiet
fi

echo "==> Restoring"
dotnet restore "$PROJECT" \
  --runtime "$RUNTIME" \
  --force \
  -p:PublishAssemblyName="$ASSEMBLY_NAME" \
  -p:PublishReadyToRun="$PUBLISH_R2R"

# The restore graph is the thing that went wrong, so check it here rather than inferring it from a
# missing file two steps later. A Host graph with no project references means the referenced projects
# were resolved under a different path spelling than the Host itself - see the pwd -P note at the top.
# EF Core Relational reaches the Host only through DlnaServer.Persistence, so its presence in the graph
# is a direct test of whether the project references were resolved. When they are dropped, the file
# describes the Host's own packages alone - 34 entries instead of 57, and no EF, SkiaSharp or SQLitePCLRaw.
ASSETS="$SCRIPT_DIR/src/DlnaServer.Host/obj/project.assets.json"

# The RID target, checked first because losing it is the easier mistake to make and the more
# confusing one to read. Any restore of this project without --runtime "$RUNTIME" replaces the
# net8.0/$RUNTIME target with a RID-less one, and publish --no-restore then fails two steps later
# with NETSDK1047 while pointing at a file that looks perfectly well-formed. A solution-wide
# `dotnet test` did exactly that, which is why the test step above now runs before the restore.
if ! grep -q "\"net8.0/$RUNTIME\"" "$ASSETS"; then
  echo "ERROR: the restore graph has no net8.0/$RUNTIME target." >&2
  echo "       $ASSETS was written by a restore that did not pass --runtime $RUNTIME," >&2
  echo "       so the publish below would fail with NETSDK1047. Something after the restore" >&2
  echo "       restored this project again without the runtime identifier - a solution-wide" >&2
  echo "       dotnet test or dotnet build with no --runtime is the known cause." >&2
  exit 1
fi

if ! grep -q "Microsoft.EntityFrameworkCore.Relational/" "$ASSETS"; then
  echo "ERROR: the restore graph is missing packages that arrive through project references." >&2
  echo "       $ASSETS describes the Host's own packages only, so EF Core, SkiaSharp and" >&2
  echo "       SQLitePCLRaw would all be absent from the publish and the server would fail at startup." >&2
  echo "       Check that the project and its ProjectReferences resolve to the same path prefix -" >&2
  echo "       a symlinked /share path resolving two ways is the known cause." >&2
  exit 1
fi

echo "==> Building (Release)"
dotnet build "$PROJECT" \
  --configuration Release \
  --runtime "$RUNTIME" \
  --no-self-contained \
  --no-restore \
  -p:Optimize=true \
  -p:Prefer32Bit=false \
  -p:DebugType=None \
  -p:PublishAssemblyName="$ASSEMBLY_NAME"

echo "==> Publishing"
dotnet publish "$PROJECT" \
  --configuration Release \
  --runtime "$RUNTIME" \
  --no-self-contained \
  --no-restore \
  --output "$PUBLISH_DIR" \
  -p:Optimize=true \
  -p:Prefer32Bit=false \
  -p:PublishAssemblyName="$ASSEMBLY_NAME" \
  -p:Deterministic=true \
  -p:PublishReadyToRun="$PUBLISH_R2R" \
  -p:InvariantGlobalization=true \
  -p:TieredCompilation=true \
  -p:StripSymbols=true \
  -p:TrimUnusedDependencies=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:UseAppHost=false

echo "==> Published to $PUBLISH_DIR"

# =========================
# VERIFY WHAT WAS PUBLISHED
# =========================
# A publish that reports success can still leave the folder incomplete, and the symptom then arrives
# much later as a runtime FileNotFoundException naming one assembly - which says nothing about how it
# went missing. Checking here turns that into a deployment failure with the name of the missing file.
#
# The list is a smoke test of the things whose absence stops startup, not the full closure: the entry
# point and its two host files, the data stack, and the two native libraries that have no managed
# fallback. Everything else fails later and more obviously.
echo "==> Verifying published output"
MISSING=""
for required in \
  "$ASSEMBLY_NAME.dll" \
  "$ASSEMBLY_NAME.deps.json" \
  "$ASSEMBLY_NAME.runtimeconfig.json" \
  "Microsoft.EntityFrameworkCore.dll" \
  "Microsoft.EntityFrameworkCore.Relational.dll" \
  "Microsoft.EntityFrameworkCore.Sqlite.dll" \
  "Microsoft.Data.Sqlite.dll" \
  "SoapCore.dll" \
  "SkiaSharp.dll" \
  "libSkiaSharp.so" \
  "libe_sqlite3.so"
do
  if [ ! -f "$PUBLISH_DIR/$required" ]; then
    MISSING="$MISSING $required"
  fi
done

DLL_COUNT="$(find "$PUBLISH_DIR" -maxdepth 1 -type f -name '*.dll' | wc -l)"
echo "==> $DLL_COUNT assemblies in $PUBLISH_DIR"

if [ -n "$MISSING" ]; then
  echo "ERROR: the publish is incomplete. Missing:$MISSING" >&2
  echo "       Delete $PUBLISH_DIR and the obj/ and bin/ folders under src/, then run again." >&2
  exit 1
fi

# =========================
# LAUNCHER
# =========================
# The exports at the top of this script live in the BUILD script's shell, so they reach the server only
# when this script also starts it. The manual command printed below used to be a bare `dotnet ...`, which
# carried none of them - so a hand-started or init-script-started server had completely different memory
# behaviour from one this script launched, silently and with nothing to compare. Emitting the launcher
# means there is one definition of the runtime environment and every start path uses it.
echo "==> Writing $PUBLISH_DIR/run.sh"
cat > "$PUBLISH_DIR/run.sh" <<RUNSH
#!/bin/sh
# Generated by NasBuild.sh - edit that script, not this file, or a redeploy overwrites your change.
cd "\$(dirname "\$0")" || exit 1

export DOTNET_GCServer=$DOTNET_GCServer
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=$DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
export MALLOC_TRIM_THRESHOLD_=$MALLOC_TRIM_THRESHOLD_
export MALLOC_MMAP_THRESHOLD_=$MALLOC_MMAP_THRESHOLD_
export MALLOC_ARENA_MAX=$MALLOC_ARENA_MAX

exec dotnet "$ASSEMBLY_NAME.dll" "\$@"
RUNSH
chmod +x "$PUBLISH_DIR/run.sh"

# =========================
# RUN
# =========================
if [ "$RUN_AFTER_PUBLISH" = "run" ]; then
  echo "==> Starting $ASSEMBLY_NAME.dll"
  cd "$PUBLISH_DIR"
  # No nohup - it is not installed on this NAS. Plain redirection captures the output and `disown`
  # detaches the job, which is what the reference script does. Without the redirect the server's
  # startup errors go to a terminal that is about to close, so a failed start leaves no trace.
  ./run.sh & disown
  echo "==> Started (pid $!)"
else
  echo "==> Not started. Run it with:"
  echo "      $PUBLISH_DIR/run.sh"
fi
