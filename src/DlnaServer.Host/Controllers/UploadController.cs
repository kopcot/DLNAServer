using System.Buffers;
using System.Data.Common;
using System.Text;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Hosting;
using DlnaServer.Core.Uploads;
using DlnaServer.Host.Uploads;
using DlnaServer.Persistence.Repositories;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Receives files from the upload page and writes them into a media folder.
    /// </summary>
    /// <remarks>
    /// A plain multipart form post, streamed part by part to disc, rather than anything travelling over
    /// the admin UI's SignalR circuit: the files this server exists to serve are films, and a circuit
    /// moves them in 32 KB messages through the server's own memory. The page that posts here is
    /// consequently static HTML with no JavaScript, which is also what makes it work from an Android
    /// phone, a Windows laptop and a Linux desktop alike.
    /// <para>
    /// <b>Mapped only when uploading was enabled at startup.</b> A server whose settings say uploads are
    /// off therefore has no route here at all, rather than a route that checks a flag - which is what
    /// makes the setting's "needs a restart" true rather than a note in the page.
    /// </para>
    /// <para>
    /// Nothing the form says is trusted. The destination is re-derived from configuration through
    /// <see cref="UploadDestination"/>, the file name is reduced to its last segment, and the extension
    /// must be one the library would index - so the worst a doctored form achieves is a rejection.
    /// Cross-site posts are refused before this runs, by <see cref="RejectCrossSiteEndpointFilter"/>.
    /// </para>
    /// <para>
    /// The route is <c>/admin/upload/files</c> and not <c>/admin/upload</c>, which is the page's. A Razor
    /// component endpoint accepts <c>POST</c> as well as <c>GET</c> - that is how static server-side forms
    /// work - so sharing the path made every post an <c>AmbiguousMatchException</c> and a 500. The
    /// <c>GET</c> was fine, because this controller has no <c>GET</c>, which is what made it look correct
    /// until a file was actually sent.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/upload/files")]
    public sealed partial class UploadController : ControllerBase
    {
        /// <summary>
        /// Large enough that a film is not copied a page at a time, and deliberately under the 85 KB
        /// threshold above which an array goes to the large object heap.
        /// </summary>
        /// <remarks>
        /// The buffer is rented rather than allocated: this server's hard constraint is memory, and an
        /// upload of fifty files would otherwise be fifty buffers for the collector to deal with. Section
        /// 3 of docs/decisions.md is the history behind that.
        /// </remarks>
        private const int CopyBufferSize = 64 * 1024;

        /// <summary>
        /// The suffix a file carries while it is still arriving.
        /// </summary>
        /// <remarks>
        /// Not in <c>MediaFileExtensions</c> and not in the catalog, so a scan that runs mid-upload walks
        /// past a half-written file instead of indexing it at whatever length it had reached.
        /// </remarks>
        private const string PartialSuffix = ".uploading";

        /// <summary>
        /// The most a form field may hold. The real ones are a folder path and a radio button.
        /// </summary>
        /// <remarks>
        /// The request body is uncapped for the files, so this is what stops one huge text field being
        /// read into memory whole.
        /// </remarks>
        private const int MaxFieldLengthInBytes = 4 * 1024;

        /// <summary>
        /// The most parts one post may carry, fields and files together.
        /// </summary>
        /// <remarks>
        /// Each part adds a line to the report, which is held in memory, so an unbounded count is an
        /// unbounded allocation even when every part is empty.
        /// </remarks>
        private const int MaxSections = 1000;

        /// <summary>
        /// How long a partial file of the same name must have been untouched before an upload removes it.
        /// </summary>
        /// <remarks>
        /// A copy in progress rewrites its partial continuously, so an hour of silence means the upload that
        /// owned it died with the process - the one exit its own <c>finally</c> cannot cover.
        /// </remarks>
        private static readonly TimeSpan _stalePartialAge = TimeSpan.FromHours(1);

        private const string DestinationCookie = "dlna.upload.destination";

        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly IUploadReportStore _reports;
        private readonly IUploadDeviceRepository _devices;
        private readonly ILibraryScanSignal _scanSignal;
        private readonly UploadSecurityLog _securityLog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UploadController> _logger;

        public UploadController(
            IOptionsMonitor<DlnaOptions> options,
            IUploadReportStore reports,
            IUploadDeviceRepository devices,
            ILibraryScanSignal scanSignal,
            UploadSecurityLog securityLog,
            TimeProvider timeProvider,
            ILogger<UploadController> logger)
        {
            _options = options;
            _reports = reports;
            _devices = devices;
            _scanSignal = scanSignal;
            _securityLog = securityLog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Takes the posted files and answers with a redirect to the report.
        /// </summary>
        /// <remarks>
        /// A redirect rather than a rendered page, so that reloading the result does not post the files a
        /// second time.
        /// </remarks>
        [HttpPost]
        [DisableFormValueModelBinding]
        public async Task<IActionResult> Upload(CancellationToken cancellationToken)
        {
            if (!MultipartRequestBoundary.TryRead(Request.ContentType, out var boundary))
            {
                return BadRequest("Expected a file upload.");
            }

            // Kestrel caps every request body at 256 KB, which is right for a SOAP Browse and impossible
            // for a film. Lifted for this one request, and replaced by a per-file limit enforced while
            // copying - a batch of ten legitimate files must not fail because their total is large.
            var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (sizeFeature is { IsReadOnly: false })
            {
                sizeFeature.MaxRequestBodySize = null;
            }

            var options = _options.CurrentValue;
            var maxFileSizeInBytes = options.Upload.MaxSizeInMegabytes * 1024L * 1024L;

            var sender = ReadSender();
            var files = new List<UploadedFile>();

            string? root = null;
            string? subFolder = null;
            var overwrite = false;
            string? destination = null;
            var written = 0;
            var sections = 0;

            // No BodyLengthLimit: it applies to every part alike, so it would cap the files as well.
            // Fields are bounded by ReadFieldAsync instead, and the part count by MaxSections.
            var reader = new MultipartReader(boundary, Request.Body);
            var section = await reader.ReadNextSectionAsync(cancellationToken);

            while (section is not null)
            {
                if (++sections > MaxSections)
                {
                    return BadRequest($"An upload may carry at most {MaxSections} files and fields.");
                }

                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                {
                    section = await reader.ReadNextSectionAsync(cancellationToken);

                    continue;
                }

                if (disposition.FileName.HasValue || disposition.FileNameStar.HasValue)
                {
                    // The form puts its fields before its files, so the destination is known by now. A
                    // post that does not is refused rather than guessed at.
                    if (destination is null
                        && !UploadDestination.TryResolve(options, root, subFolder, out destination, out var problem))
                    {
                        return BadRequest(problem);
                    }

                    var file = await ReceiveAsync(
                        section,
                        disposition,
                        destination,
                        options.Library,
                        maxFileSizeInBytes,
                        overwrite,
                        sender,
                        cancellationToken);

                    files.Add(file);

                    if (file.Outcome is UploadOutcome.Uploaded or UploadOutcome.Overwritten)
                    {
                        written++;
                    }
                }
                else if (disposition.Name.HasValue)
                {
                    var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

                    // A field this form does not have is never read: the next ReadNextSectionAsync drains
                    // it without buffering.
                    if (name is "root" or "subFolder" or "existing")
                    {
                        var value = await ReadFieldAsync(section, cancellationToken);

                        if (value is null)
                        {
                            return BadRequest($"A form field may hold at most {MaxFieldLengthInBytes} bytes.");
                        }

                        switch (name)
                        {
                            case "root":
                                root = value;
                                break;

                            case "subFolder":
                                subFolder = value;
                                break;

                            case "existing":
                                overwrite = string.Equals(value, "overwrite", StringComparison.Ordinal);
                                break;

                            default:
                                break;
                        }
                    }
                }

                section = await reader.ReadNextSectionAsync(cancellationToken);
            }

            if (destination is null
                && !UploadDestination.TryResolve(options, root, subFolder, out destination, out var unresolved))
            {
                return BadRequest(unresolved);
            }

            var report = new UploadReport
            {
                Id = Guid.NewGuid(),
                CompletedUtc = DateTimeOffset.UtcNow,
                Destination = destination,
                Files = files,
            };

            _reports.Add(report);

            if (written > 0)
            {
                // Asked for once for the batch, and never awaited: a pass over a large library takes
                // minutes, and the request would time out holding the connection open for it.
                // Asked for before remembering the device, so a failure there cannot cost the scan.
                _scanSignal.RequestScan();

                await RememberAsync(sender, destination, written, cancellationToken);
            }

            LogFinished(destination, files.Count, written);

            return SeeOther(report.Id);
        }

        /// <summary>
        /// Reads one file part to disc, deciding first whether it should be taken at all.
        /// </summary>
        private async Task<UploadedFile> ReceiveAsync(
            MultipartSection section,
            ContentDispositionHeaderValue disposition,
            string destination,
            LibraryOptions library,
            long maxFileSizeInBytes,
            bool overwrite,
            Sender sender,
            CancellationToken cancellationToken)
        {
            var raw = disposition.FileNameStar.HasValue
                ? disposition.FileNameStar.Value
                : HeaderUtilities.RemoveQuotes(disposition.FileName).Value;

            var fileName = UploadFileName.Sanitise(raw);

            if (fileName.Length == 0)
            {
                return Refuse(sender, destination, raw ?? "(no name)", "The file has no usable name.");
            }

            if (!UploadFileName.IsAcceptedMedia(library, fileName))
            {
                return Refuse(
                    sender,
                    destination,
                    fileName,
                    "That kind of file is not one this library holds. Add its extension on the Settings "
                        + "page if it should be.");
            }

            var target = Path.Combine(destination, fileName);
            var exists = System.IO.File.Exists(target);

            if (exists && !overwrite)
            {
                return Record(
                    sender,
                    destination,
                    fileName,
                    sizeInBytes: 0,
                    UploadOutcome.Skipped,
                    "A file of that name was already there.");
            }

            // Unique to this part, so two uploads of the same name at once cannot delete each other's file
            // mid-copy. The suffix stays last, which is what keeps a scan walking past it.
            var partialPath = $"{target}.{Guid.NewGuid():N}{PartialSuffix}";
            var isMoved = false;

            try
            {
                _ = Directory.CreateDirectory(destination);

                var size = await CopyAsync(section.Body, partialPath, maxFileSizeInBytes, cancellationToken);

                if (size < 0)
                {
                    return Refuse(
                        sender,
                        destination,
                        fileName,
                        $"The file is larger than the {maxFileSizeInBytes / (1024L * 1024L)} MB limit.");
                }

                // Moved into place only once every byte is down, so the watcher sees one arrival rather
                // than a file that keeps growing.
                try
                {
                    System.IO.File.Move(partialPath, target, overwrite);
                }
                catch (IOException) when (!overwrite && System.IO.File.Exists(target))
                {
                    // Another upload of the same name finished while this one was copying, and the
                    // operator asked for existing files to be kept.
                    return Record(
                        sender,
                        destination,
                        fileName,
                        sizeInBytes: 0,
                        UploadOutcome.Skipped,
                        "A file of that name was already there.");
                }

                isMoved = true;
                DeleteStalePartials(destination, fileName);

                return Record(
                    sender,
                    destination,
                    fileName,
                    size,
                    exists ? UploadOutcome.Overwritten : UploadOutcome.Uploaded,
                    reason: null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogWriteFailed(fileName, destination, exception);

                return Refuse(sender, destination, fileName, exception.Message);
            }
            finally
            {
                // Every way out but the move - too large, a write error, a skip, and a client that went
                // away mid-copy, which arrives as an exception no catch above takes.
                if (!isMoved)
                {
                    Delete(partialPath);
                }
            }
        }

        /// <summary>
        /// Copies the part to the given path, returning the byte count, or -1 once it is too large.
        /// </summary>
        private static async Task<long> CopyAsync(
            Stream body,
            string path,
            long maxFileSizeInBytes,
            CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            var total = 0L;

            try
            {
                // bufferSize 1 disables the stream's own buffer: the loop below already reads in
                // 64 KB blocks, and a second buffer would only copy each block once more.
                // CreateNew rather than Create: Create follows a symlink sitting at this name and writes
                // through it, so a planted link would be the file that got overwritten. The caller makes the
                // name unique to this part, so nothing of this server's own is ever there to clear first.
                await using var file = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);

                while (true)
                {
                    var read = await body.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    total += read;

                    if (total > maxFileSizeInBytes)
                    {
                        return -1;
                    }

                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return total;
        }

        /// <summary>
        /// Reads a form field, or returns null once it passes <see cref="MaxFieldLengthInBytes"/>.
        /// </summary>
        private static async Task<string?> ReadFieldAsync(
            MultipartSection section,
            CancellationToken cancellationToken)
        {
            // Small by construction - these are a folder path and a radio button - so reading stops one
            // byte past the limit rather than buffering whatever a doctored form sends.
            var buffer = ArrayPool<byte>.Shared.Rent(MaxFieldLengthInBytes + 1);
            var total = 0;

            try
            {
                while (total <= MaxFieldLengthInBytes)
                {
                    var read = await section.Body.ReadAsync(
                        buffer.AsMemory(total, MaxFieldLengthInBytes + 1 - total),
                        cancellationToken);

                    if (read == 0)
                    {
                        return Encoding.UTF8.GetString(buffer, 0, total).Trim();
                    }

                    total += read;
                }

                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void Delete(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Tidying up after a failure must not replace the failure being reported.
            }
        }

        // Partials are uniquely named, so a crashed upload's leftover is no longer overwritten by the next
        // upload of that name; this is where it goes instead. Only this name's, and only once long idle.
        private void DeleteStalePartials(string destination, string fileName)
        {
            var staleBefore = _timeProvider.GetUtcNow().UtcDateTime - _stalePartialAge;
            var prefix = fileName + ".";

            try
            {
                // Filtered by prefix here rather than in the pattern: '*' and '?' are legal in a Linux file
                // name, and inside a search pattern they would widen it to other files' partials.
                foreach (var partial in Directory.EnumerateFiles(destination, $"*{PartialSuffix}"))
                {
                    if (Path.GetFileName(partial).StartsWith(prefix, StringComparison.Ordinal)
                        && System.IO.File.GetLastWriteTimeUtc(partial) < staleBefore)
                    {
                        Delete(partial);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Housekeeping only - the upload itself has already succeeded.
            }
        }

        private UploadedFile Refuse(Sender sender, string destination, string fileName, string reason)
        {
            return Record(sender, destination, fileName, sizeInBytes: 0, UploadOutcome.Failed, reason);
        }

        /// <summary>
        /// Writes one line to the security log and returns the line the report will show.
        /// </summary>
        /// <remarks>
        /// The two are produced together so they cannot disagree: the log is what remains after the
        /// report has lapsed, and a file recorded as written in one and refused in the other would make
        /// both useless.
        /// </remarks>
        private UploadedFile Record(
            Sender sender,
            string destination,
            string fileName,
            long sizeInBytes,
            UploadOutcome outcome,
            string? reason)
        {
            _securityLog.Record(
                sender.RemoteAddress,
                sender.UserAgent,
                sender.AcceptLanguage,
                sender.Fingerprint,
                destination,
                fileName,
                sizeInBytes,
                outcome,
                reason ?? string.Empty);

            return new UploadedFile
            {
                FileName = fileName,
                SizeInBytes = sizeInBytes,
                Outcome = outcome,
                Reason = reason,
            };
        }

        private Sender ReadSender()
        {
            var remoteAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();
            var acceptLanguage = Request.Headers.AcceptLanguage.ToString();

            return new Sender(
                remoteAddress,
                userAgent.Length == 0 ? "unknown" : userAgent,
                acceptLanguage,
                UploadDeviceFingerprint.Create(remoteAddress, userAgent));
        }

        /// <summary>
        /// Remembers the destination twice - in the browser, and in the database against the device.
        /// </summary>
        /// <remarks>
        /// The cookie is what normally answers, and the row is what answers when the cookie has been
        /// cleared. Neither is allowed to fail the upload: the files are already on disc by this point,
        /// so a database that will not write is reported and nothing more.
        /// </remarks>
        private async Task RememberAsync(
            Sender sender,
            string destination,
            int written,
            CancellationToken cancellationToken)
        {
            Response.Cookies.Append(DestinationCookie, destination, new CookieOptions
            {
                HttpOnly = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
            });

            try
            {
                await _devices.RecordAsync(
                    new UploadDeviceUpdateDto
                    {
                        Fingerprint = sender.Fingerprint,
                        RemoteAddress = sender.RemoteAddress,
                        UserAgent = sender.UserAgent,
                        AcceptLanguage = sender.AcceptLanguage.Length == 0 ? null : sender.AcceptLanguage,
                        Destination = destination,
                        FileCount = written,
                    },
                    cancellationToken);
            }
            catch (DbException exception)
            {
                // DbException rather than an EF type on purpose: only Persistence may reference Entity
                // Framework, and an architecture test fails the build if the host reaches for one.
                LogDeviceNotRemembered(sender.Fingerprint, exception);
            }
        }

        /// <summary>
        /// 303, which is the redirect that turns a post into a get.
        /// </summary>
        private StatusCodeResult SeeOther(Guid reportId)
        {
            Response.Headers.Location = $"/admin/upload?report={reportId}";

            return StatusCode(StatusCodes.Status303SeeOther);
        }

        private sealed record Sender(
            string RemoteAddress,
            string UserAgent,
            string AcceptLanguage,
            string Fingerprint);
    }
}
