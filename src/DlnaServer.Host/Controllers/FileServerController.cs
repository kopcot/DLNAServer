using CommunityToolkit.HighPerformance;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Persistence.Repositories;
using DlnaServer.Upnp.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Serves media files and their thumbnails. These are the URLs a DIDL-Lite item points at, so this
    /// is where a renderer actually fetches what it plays.
    /// </summary>
    /// <remarks>
    /// Responses stream from the disc or from the served-bytes cache, and always with range support -
    /// a renderer seeks by asking for a byte range, and a server that ignores <c>Range</c> forces it to
    /// re-download from the start.
    /// <para>
    /// No response compression is registered for this application. The reference registers it globally,
    /// which is hazardous over <c>206 Partial Content</c>: compressing a range changes the byte count the
    /// renderer was promised in <c>Content-Range</c>, and it wastes cycles on media that is already
    /// compressed.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("fileserver")]
    public sealed partial class FileServerController : ControllerBase
    {
        /// <summary>
        /// Labels for the log line naming where a response came from.
        /// </summary>
        private const string SourceCache = "cache";
        private const string SourceDisc = "disc";
        private const string SourceDatabase = "database";

        private readonly IMediaFileRepository _files;
        private readonly IServedFileCache _cache;
        private readonly IMediaContentResolver _content;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<FileServerController> _logger;

        public FileServerController(
            IMediaFileRepository files,
            IServedFileCache cache,
            IMediaContentResolver content,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<FileServerController> logger)
        {
            _files = files;
            _cache = cache;
            _content = content;
            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// Streams one media file.
        /// </summary>
        /// <remarks>
        /// A file not yet in the cache is served from the disc and queued to be read behind this
        /// response - never waited for. The first request touches the platter either way, so blocking on
        /// the cache would add latency and save nothing. Every later request is served from memory, which
        /// is the point: the NAS drives are audible, and a file already sent should not wake one again.
        /// <para>
        /// HEAD has its own action below. MVC does not route HEAD to a GET action on its own - the
        /// reference answers those probes with 405 - but sharing this one made a probe queue a
        /// whole-file read; see <see cref="HeadFile"/>.
        /// </para>
        /// </remarks>
        [HttpGet("file/{id:guid}")]
        public async Task<IActionResult> GetFile([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var file = await _files.GetByPublicIdAsync(id, cancellationToken);

            if (file is null)
            {
                LogFileNotIndexed(id, HttpContext.Connection.RemoteIpAddress?.ToString());
                return NotFound();
            }

            if (_options.CurrentValue.Compatibility.SendDlnaResponseHeaders)
            {
                DlnaResponseHeaders.Apply(
                    Request,
                    Response,
                    file.Mime.ToMedia(),
                    DlnaProtocolInfo.ContentFeaturesFor(file.Mime, file.DlnaProfileName, file.Extension));
            }

            var contentType = file.Mime.ToMimeString();

            // Shared with the admin UI's /admin/media, so a film behaves the same whichever door it came
            // through: served from memory when held, and queued to be read into memory when not.
            var source = _content.Resolve(file);

            if (source.IsCached)
            {
                LogServed(file.FullPath, SourceCache);

                // AsStream over the cached memory: there is no File(ReadOnlyMemory<byte>) overload, and
                // the alternative - ToArray - would copy the whole payload on every range request.
                return File(source.Content.AsStream(), contentType, enableRangeProcessing: true);
            }

            // After the cache, not before it, which is what IServedFileCache.Evict promises: deletion is
            // deliberately not a reason to evict, so a television mid-stream is not cut off because the
            // file went away. Checking first turned a still-cached, still-streamable film into a 404 on
            // the one port a television uses. /admin/media had the order right all along.
            if (!System.IO.File.Exists(file.FullPath))
            {
                // Indexed but gone from disc. Reconciliation will remove it on the next scan.
                LogFileMissing(file.FullPath);
                return NotFound();
            }

            LogServed(file.FullPath, SourceDisc);
            return PhysicalFile(file.FullPath, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Answers a renderer's probe for a media file's size and type, without reading it.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="GetFile"/> deliberately. Sharing that action meant a HEAD probe ran
        /// its whole body, including the queue that reads the file into the byte cache - so probing a
        /// film the renderer then chose not to play still pulled up to <c>MaxFileSizeInMegabytes</c> into
        /// memory. This never touches the backlog, and reads the cache only to answer for a file that has
        /// been deleted from disc while its bytes are still held.
        /// <para>
        /// <see cref="ControllerBase.PhysicalFile(string, string, bool)"/> is still the return: for a
        /// HEAD request MVC writes the headers - <c>Content-Length</c>, <c>Content-Type</c> and
        /// <c>Accept-Ranges</c> - and suppresses the body, so the length comes from the directory entry
        /// rather than from reading anything.
        /// </para>
        /// </remarks>
        [HttpHead("file/{id:guid}")]
        public async Task<IActionResult> HeadFile([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var file = await _files.GetByPublicIdAsync(id, cancellationToken);

            if (file is null)
            {
                LogFileNotIndexed(id, HttpContext.Connection.RemoteIpAddress?.ToString());
                return NotFound();
            }

            var cached = ReadOnlyMemory<byte>.Empty;
            var isOnDisc = System.IO.File.Exists(file.FullPath);

            // A file deleted from disc but still held in memory is one this server can still serve, which
            // is what IServedFileCache.Evict promises: deletion is deliberately not a reason to evict, so
            // a television mid-stream is not cut off. Answering 404 here while GetFile answers 200 from
            // the cache would stop playback at the probe - most renderers HEAD before they GET.
            // TryGet is a read: it queues nothing and never touches the backlog, which is the cost this
            // action exists to avoid.
            if (!isOnDisc && !_cache.TryGet(file.FullPath, out cached))
            {
                LogFileMissing(file.FullPath);
                return NotFound();
            }

            if (_options.CurrentValue.Compatibility.SendDlnaResponseHeaders)
            {
                DlnaResponseHeaders.Apply(
                    Request,
                    Response,
                    file.Mime.ToMedia(),
                    DlnaProtocolInfo.ContentFeaturesFor(file.Mime, file.DlnaProfileName, file.Extension));
            }

            LogProbed(file.FullPath);

            var contentType = file.Mime.ToMimeString();

            // PhysicalFile takes the length from the directory entry, which a deleted file no longer has -
            // so the cached payload has to answer for its own Content-Length.
            return isOnDisc
                ? PhysicalFile(file.FullPath, contentType, enableRangeProcessing: true)
                : File(cached.AsStream(), contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Returns one generated thumbnail.
        /// </summary>
        /// <remarks>
        /// Sources in order: the served-bytes cache, then the database copy when one was stored, then the
        /// file in the thumbnail cache directory. Preferring the database over the file reproduces the
        /// reference, and it is why <c>Thumbnails.StoreInDatabase</c> is worth setting - the row is
        /// reachable through SQLite's own page cache while the file is a separate seek.
        /// <para>
        /// <c>Cache-Control</c> is <c>private</c> rather than <c>public</c>, so the response-caching
        /// middleware does not keep a second copy of a payload this server already holds in the
        /// served-bytes cache. The renderer still caches it for the hour.
        /// </para>
        /// </remarks>
        [HttpGet("thumbnail/{id:guid}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetThumbnail([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var thumbnail = await _files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            if (thumbnail is null)
            {
                LogThumbnailNotIndexed(id, HttpContext.Connection.RemoteIpAddress?.ToString());
                return NotFound();
            }

            if (_options.CurrentValue.Compatibility.SendDlnaResponseHeaders)
            {
                DlnaResponseHeaders.Apply(
                    Request,
                    Response,
                    DlnaMedia.Image,
                    DlnaProtocolInfo.ContentFeaturesForThumbnail(
                        thumbnail.Mime,
                        DlnaProtocolInfo.ThumbnailProfileName));
            }

            var contentType = thumbnail.Mime.ToMimeString();

            if (_cache.TryGet(thumbnail.FilePath, out var cached))
            {
                LogServedThumbnail(thumbnail.FilePath, SourceCache);
                return File(cached.AsStream(), contentType, enableRangeProcessing: true);
            }

            if (thumbnail.HasStoredContent)
            {
                var stored = await _files.GetThumbnailContentAsync(id, cancellationToken);

                if (stored is not null)
                {
                    // Keyed by the file path, so a later request hits memory whichever source filled it.
                    // AsMemory is a view over the array the repository already returned, not a copy.
                    _cache.Store(thumbnail.FilePath, CachedContentClass.Thumbnail, stored.AsMemory());

                    LogServedThumbnail(thumbnail.FilePath, SourceDatabase);

                    // AsMemory().AsStream() rather than the byte[] overload: both avoid a copy, but this
                    // keeps every cached-payload response on one path, so the serving shape does not
                    // depend on which source happened to fill the cache.
                    return File(stored.AsMemory().AsStream(), contentType, enableRangeProcessing: true);
                }
            }

            var loaded = await _cache.LoadAsync(thumbnail.FilePath, CachedContentClass.Thumbnail, cancellationToken);

            if (!loaded.IsEmpty)
            {
                LogServedThumbnail(thumbnail.FilePath, SourceCache);
                return File(loaded.AsStream(), contentType, enableRangeProcessing: true);
            }

            if (!System.IO.File.Exists(thumbnail.FilePath))
            {
                LogThumbnailMissing(thumbnail.FilePath);
                return NotFound();
            }

            LogServedThumbnail(thumbnail.FilePath, SourceDisc);
            return PhysicalFile(thumbnail.FilePath, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Answers a probe for a thumbnail's size and type.
        /// </summary>
        /// <remarks>
        /// The recorded size is authoritative and is what <see cref="DlnaServer.Core.Contracts.ThumbnailDto.SizeInBytes"/> holds,
        /// so neither the database copy nor the file has to be read to answer. Split from
        /// <see cref="GetThumbnail"/> for the same reason as <see cref="HeadFile"/>.
        /// </remarks>
        [HttpHead("thumbnail/{id:guid}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> HeadThumbnail([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var thumbnail = await _files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            if (thumbnail is null)
            {
                LogThumbnailNotIndexed(id, HttpContext.Connection.RemoteIpAddress?.ToString());
                return NotFound();
            }

            if (_options.CurrentValue.Compatibility.SendDlnaResponseHeaders)
            {
                DlnaResponseHeaders.Apply(
                    Request,
                    Response,
                    DlnaMedia.Image,
                    DlnaProtocolInfo.ContentFeaturesForThumbnail(
                        thumbnail.Mime,
                        DlnaProtocolInfo.ThumbnailProfileName));
            }

            Response.ContentType = thumbnail.Mime.ToMimeString();
            Response.ContentLength = thumbnail.SizeInBytes;

            LogProbed(thumbnail.FilePath);

            return new EmptyResult();
        }

        /// <summary>
        /// One line per response, at a level that keeps a whole film from filling the log.
        /// </summary>
        /// <remarks>
        /// A renderer issues one request per byte range, so a single playback produces many of these. Both
        /// are at Information - see the note on the declarations for what that costs and why it was chosen.
        /// </remarks>
        private void LogServed(string filePath, string source)
        {
            if (Request.Headers.Range.Count > 0)
            {
                LogServingRange(filePath, source, Request.Headers.Range.ToString());
                return;
            }

            LogServingTransfer(filePath, source);
        }

        /// <remarks>
        /// The same fan-out for previews, kept separate only so they can stay at Debug while the media
        /// lines are at Information.
        /// </remarks>
        private void LogServedThumbnail(string filePath, string source)
        {
            if (Request.Headers.Range.Count > 0)
            {
                LogServingThumbnailRange(filePath, source, Request.Headers.Range.ToString());
                return;
            }

            LogServingThumbnailTransfer(filePath, source);
        }
    }
}
