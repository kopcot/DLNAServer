# Third-party notices

DlnaServer is distributed under the [MIT licence](LICENSE). It builds on the packages below, each of
which carries its own terms. The versions are the ones pinned in `Directory.Packages.props`; that file is
the authority, and this list is regenerated from it rather than edited by hand.

Nothing here is redistributed in source form. The published output carries the compiled assemblies of
these packages and, for SkiaSharp, its native library.

## Shipped with the server

| Package | Version | Licence |
| --- | --- | --- |
| CommunityToolkit.HighPerformance | 8.4.2 | MIT |
| Microsoft.EntityFrameworkCore.Design | 9.0.10 | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.10 | MIT |
| Microsoft.Extensions.Caching.Memory | 9.0.10 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | MIT |
| MetadataExtractor | 2.9.3 | Apache-2.0 |
| Serilog.AspNetCore | 9.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 |
| SkiaSharp | 4.152.1 | MIT |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.152.1 | MIT |
| SoapCore | 1.2.1.11 | MIT |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0, declared as a licence file in the package rather than an SPDX expression |
| System.Security.Cryptography.Xml | 8.0.4 | MIT |
| Xabe.FFmpeg | 6.0.2 | Custom, by URL - see below |
| Xabe.FFmpeg.Downloader | 6.0.2 | Custom, by URL - see below |

## Build and test only

Not part of any deployment. Listed for completeness.

| Package | Version | Licence |
| --- | --- | --- |
| FluentAssertions | 8.11.0 | Licence file in the package - see below |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.11 | MIT |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT |
| NetArchTest.Rules | 1.3.2 | MIT, not declared in the package metadata |
| NUnit | 4.6.1 | MIT |
| NUnit.Analyzers | 4.15.0 | MIT |
| NUnit3TestAdapter | 6.3.0 | MIT |
| coverlet.collector | 10.0.1 | MIT |

## Two that are not simply permissive

**Xabe.FFmpeg** is dual-licensed and its free tier is non-commercial; a commercial deployment needs a
licence from Xabe. It is used for metadata extraction and video previews.

**FluentAssertions** changed to a paid licence for commercial use at version 8. It is a test-only
dependency, so it is not distributed with the server, but a commercial build pipeline is still a use of
it. Version 7 remains under the previous terms if that matters.

**ffmpeg itself is not distributed with this server.** `Thumbnails.DownloadFFmpeg` ships off, and the
binaries an operator supplies in the `ffmpeg` folder carry their own licence - typically LGPL or GPL
depending on the build. That choice, and its consequences, belong to whoever puts them there.

## How this list was checked

Each row was read from the `<license>` element of the package's own `.nuspec` in the local NuGet cache,
not from memory. Three do not declare a plain SPDX expression and are called out in the table for that
reason: SQLitePCLRaw ships a licence file, Xabe.FFmpeg points at a URL, and NetArchTest declares nothing.

## Verifying this list

`dotnet list package --include-transitive` enumerates everything actually resolved, including the
transitive packages this table does not name. `NuGetAudit` runs in `all` mode on every build, so an
advisory in any of them fails CI rather than waiting to be noticed.
