<#
.SYNOPSIS
    Starts the server, lets it settle, and reports memory as a Markdown table row.

.DESCRIPTION
    Run after every milestone - or every substantial part of one - and paste the row into the trend
    table in PLAN.md section 3b. A single reading says nothing; the trend across milestones is what
    shows whether the memory budget is holding as features land.

    Working set is the number that matters on the NAS. Managed heap alone is misleading: the reference
    server shows roughly 42 MB managed against a 395 MB working set, so most of its footprint is native
    (SkiaSharp, SQLite, allocator fragmentation) and never appears in GC statistics.

.PARAMETER Label
    What was just finished, e.g. "M6 (streaming)". Appears in the first column.

.PARAMETER SettleSeconds
    How long to let the server run before measuring. At least 60; startup indexing skews anything shorter.

.PARAMETER Configuration
    Build configuration to run. Release is what the NAS runs and reports lower numbers than Debug.

.EXAMPLE
    ./tools/measure-memory.ps1 -Label "M6 (streaming)"

.EXAMPLE
    ./tools/measure-memory.ps1 -Label "M6 Release" -Configuration Release -SettleSeconds 120
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Label,

    [ValidateRange(60, 3600)]
    [int] $SettleSeconds = 60,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    # Emulates a constrained box. Section 3's own rule makes a reading inadmissible unless it was taken
    # under a 2-4 GB limit, and this script had no way to set one - so every row in the trend table was
    # taken on a ~40 GB dev machine and none of them qualified. The heap limit is passed to the child
    # process as an environment variable, and the value used is recorded in the row below so a reading can
    # be audited after the fact rather than taken on trust.
    [ValidateRange(0, 100)]
    [int] $HeapLimitPercent = 0,

    # The byte cache clamps its budget to a fraction of GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
    # which a heap limit REWRITES - so emulating a smaller box silently shrinks the largest consumer and
    # the reading then understates a real machine. Pinning the budget keeps the thing being measured the
    # same size as it is in production.
    [ValidateRange(0, 4096)]
    [int] $PinFileCacheMegabytes = 0
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $repoRoot 'src\DlnaServer.Host'

# The port the media surface listens on, which is where /manage/memory lives.
$config = Get-Content (Join-Path $hostProject 'config.json') -Raw | ConvertFrom-Json
$port = $config.Dlna.Server.Port

Write-Host "Building ($Configuration)..."
dotnet build (Join-Path $repoRoot 'DlnaServer.sln') -c $Configuration --nologo | Out-Null

$heapNote = 'no heap limit (INADMISSIBLE per section 3)'

if ($HeapLimitPercent -gt 0) {
    $env:DOTNET_GCHeapHardLimitPercent = ('{0:X}' -f $HeapLimitPercent)
    $heapNote = "heap limit ${HeapLimitPercent}%"
    Write-Host "Emulating a constrained box: $heapNote"
} else {
    Write-Warning 'No -HeapLimitPercent given. This reading is inadmissible under PLAN.md section 3.'
}

if ($PinFileCacheMegabytes -gt 0) {
    $env:Dlna__FileCache__MaxTotalSizeInMegabytes = $PinFileCacheMegabytes
    $heapNote = "$heapNote, file cache pinned to ${PinFileCacheMegabytes} MB"
    Write-Host "Pinning the file-cache budget to $PinFileCacheMegabytes MB"
}

Write-Host "Starting the server on port $port..."
$server = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--no-build', '-c', $Configuration) `
    -WorkingDirectory $hostProject `
    -PassThru -WindowStyle Hidden

try {
    Write-Host "Letting it settle for $SettleSeconds seconds..."
    Start-Sleep -Seconds $SettleSeconds

    $memory = Invoke-RestMethod -Uri "http://localhost:$port/manage/memory" -TimeoutSec 15

    $gen = '{0} / {1} / {2}' -f $memory.gen0Collections, $memory.gen1Collections, $memory.gen2Collections
    $gcMode = if ($memory.isServerGc) { 'Server GC' } else { 'Workstation GC' }
    # The heap-limit state is part of the row, not a side note: a reading whose admissibility cannot be
    # read off the table is a reading nobody can check later.
    $notes = '{0} build, {1}, {2} threads, {3}.' -f $Configuration, $gcMode, $memory.threadCount, $heapNote

    Write-Host ''
    Write-Host 'Paste this row into PLAN.md section 3b:' -ForegroundColor Green
    Write-Host ''
    '| {0} | {1} s | **{2} MB** | {3} MB | {4} MB | {5} | {6} | {7} |' -f `
        $Label,
        [int] $memory.uptimeSeconds,
        $memory.workingSetMb,
        $memory.privateMemoryMb,
        $memory.managedHeapMb,
        $gen,
        $heapNote,
        $notes
}
finally {
    if ($server -and -not $server.HasExited) {
        Write-Host ''
        Write-Host 'Stopping the server...'
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }

    # dotnet run spawns the app as a child; make sure it goes too.
    Get-Process -Name 'DlnaServer.Host' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
