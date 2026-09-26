using System.IO.Compression;
using CommunityToolkit.HighPerformance;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Host.Delivery;
using DlnaServer.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Serves media and thumbnails to the admin UI, on the admin port.
    /// </summary>
    /// <remarks>
    /// The admin pages deliberately do <b>not</b> link to the media port. Doing so would put the
    /// renderer-facing endpoints and the port they live on into the browser's address bar and network
    /// tab, which is exactly what an operator interface should not advertise - and it would fail outright
    /// wherever the admin port is reachable and the media port is not.
    /// <para>
    /// So this reads the same sources as <see cref="FileServerController"/> - the served-bytes cache, the
    /// database copy, then the file - and serves them under the admin port instead. It is a second door
    /// onto the same content rather than a forwarding hop: an HTTP call to the other port would leave the
    /// media surface just as reachable while adding a round trip.
    /// </para>
    /// <para>
    /// No DLNA response headers here. Those describe a renderer's transfer contract and mean nothing to a
    /// browser, whereas range support does - it is what lets an operator seek in a video.
    /// </para>
    /// <para>
    /// Caching behaviour is <b>identical</b> to the media port, through the shared
    /// <see cref="Delivery.IMediaContentResolver"/>: a film watched here is the same workload as one
    /// watched on a television, so it is served from memory when held and queued to be read into memory
    /// when not. Having the two differ would make the acoustic goal depend on which door the request came
    /// through.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/media")]
    public sealed partial class AdminMediaController : ControllerBase
    {
        private readonly IMediaFileRepository _files;
        private readonly ISubtitleRepository _subtitles;
        private readonly ISubtitleFileChecker _subtitleChecker;
        private readonly IServedFileCache _cache;
        private readonly IMediaContentResolver _content;
        private readonly ILogger<AdminMediaController> _logger;

        public AdminMediaController(
            IMediaFileRepository files,
            ISubtitleRepository subtitles,
            ISubtitleFileChecker subtitleChecker,
            IServedFileCache cache,
            IMediaContentResolver content,
            ILogger<AdminMediaController> logger)
        {
            _files = files;
            _subtitles = subtitles;
            _subtitleChecker = subtitleChecker;
            _cache = cache;
            _content = content;
            _logger = logger;
        }

        /// <summary>
        /// Streams one media file for the preview player.
        /// </summary>
        /// <remarks>
        /// Range processing is on, which is what makes seeking work in a browser's video element. The
        /// cache is read <b>and filled</b>, exactly as the media port does - see the note on the class.
        /// </remarks>
        [HttpGet("file/{id:guid}")]
        public async Task<IActionResult> GetFile([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var file = await _files.GetByPublicIdAsync(id, cancellationToken);

            if (file is null)
            {
                return NotFound();
            }

            var contentType = file.Mime.ToMimeString();
            var source = _content.Resolve(file);

            ScriptableContentHeaders.Apply(Response, file.Mime);

            if (source.IsCached)
            {
                return File(source.Content.AsStream(), contentType, enableRangeProcessing: true);
            }

            if (!System.IO.File.Exists(file.FullPath))
            {
                LogMissing(file.FullPath);
                return NotFound();
            }

            return PhysicalFile(file.FullPath, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Answers a browser's probe for a preview file's size and type, without reading it.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="GetFile"/> for the same reason
        /// <c>FileServerController.HeadFile</c> is, and this action was missing while that one existed.
        /// Sharing the GET action meant a HEAD ran its whole body including <c>Resolve</c>, whose
        /// documented side effect is queueing the file into the byte cache - so a browser's media
        /// element or a preload probe on the preview page pulled up to
        /// <c>MaxFileSizeInMegabytes</c> into memory for a film nobody watched, in the subsystem the
        /// memory budget is fighting over. This touches neither the cache nor the backlog.
        /// </remarks>
        [HttpHead("file/{id:guid}")]
        public async Task<IActionResult> HeadFile([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var file = await _files.GetByPublicIdAsync(id, cancellationToken);

            if (file is null)
            {
                return NotFound();
            }

            var cached = ReadOnlyMemory<byte>.Empty;
            var isOnDisc = System.IO.File.Exists(file.FullPath);

            // A file deleted from disc but still held in memory is one GetFile still serves, so the probe
            // must agree - FileServerController.HeadFile answers the same way. TryGet is a read: it
            // queues nothing, which is the cost this action exists to avoid.
            if (!isOnDisc && !_cache.TryGet(file.FullPath, out cached))
            {
                LogMissing(file.FullPath);
                return NotFound();
            }

            ScriptableContentHeaders.Apply(Response, file.Mime);

            var contentType = file.Mime.ToMimeString();

            // MVC writes Content-Length, Content-Type and Accept-Ranges for a HEAD and suppresses the
            // body, so the length comes from the directory entry rather than from reading anything.
            // A deleted file has no directory entry, so the cached payload answers for its own length.
            return isOnDisc
                ? PhysicalFile(file.FullPath, contentType, enableRangeProcessing: true)
                : File(cached.AsStream(), contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Answers a probe for a thumbnail without reading one.
        /// </summary>
        /// <remarks>
        /// The mirror of the gap <see cref="HeadFile"/> was added to close, and it was still open here:
        /// MVC does not route HEAD to a GET action, so a browser preloading a tile got 405 where the
        /// media port answers cheaply. The recorded size is authoritative - <c>ThumbnailDto.SizeInBytes</c>
        /// holds it - so neither the database copy nor the file has to be touched, which is the whole
        /// point of answering a probe separately.
        /// </remarks>
        [HttpHead("thumbnail/{id:guid}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> HeadThumbnail([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var thumbnail = await _files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            if (thumbnail is null)
            {
                return NotFound();
            }

            Response.ContentType = thumbnail.Mime.ToMimeString();
            Response.ContentLength = thumbnail.SizeInBytes;

            return new EmptyResult();
        }

        /// <summary>
        /// Returns one file's thumbnail for the folder tiles.
        /// </summary>
        [HttpGet("thumbnail/{id:guid}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetThumbnail([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var thumbnail = await _files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            if (thumbnail is null)
            {
                return NotFound();
            }

            // The same ladder as the media port's, through the resolver both share - the two used to be
            // hand-kept copies and had already drifted once.
            var contentType = thumbnail.Mime.ToMimeString();
            var (source, content) = await _content.ResolveThumbnailAsync(thumbnail, _files, cancellationToken);

            return source switch
            {
                ThumbnailSource.None => NotFound(),
                ThumbnailSource.Disc => PhysicalFile(thumbnail.FilePath, contentType, enableRangeProcessing: true),
                _ => File(content.AsStream(), contentType, enableRangeProcessing: true),
            };
        }

        /// <summary>
        /// Downloads the subtitle and lyrics files linked to one media file - the file itself when there is
        /// one, a zip of all of them when there are several.
        /// </summary>
        /// <remarks>
        /// Only links that pass <see cref="ISubtitleFileChecker"/> are offered, the same rule the file's page
        /// uses to decide which button to show, so the page and this answer agree on the count. Zip entries
        /// keep their path from the media file's folder, which keeps <c>film.en.srt</c> and
        /// <c>Subs/film.en.srt</c> apart.
        /// </remarks>
        [HttpGet("subtitles/{id:guid}")]
        public async Task<IActionResult> GetSubtitles([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var file = await _files.GetByPublicIdAsync(id, cancellationToken);

            if (file is null)
            {
                return NotFound();
            }

            var mediaDirectory = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
            var links = await _subtitles.GetForFilesAsync([id], cancellationToken);
            var usable = new List<string>();

            if (links.TryGetValue(id, out var found))
            {
                foreach (var link in found)
                {
                    if (_subtitleChecker.TryCheck(mediaDirectory, link.RelativePath, out var relativePath, out _))
                    {
                        usable.Add(relativePath);
                    }
                }
            }

            if (usable.Count == 0)
            {
                return NotFound();
            }

            if (usable.Count == 1)
            {
                var fullPath = Path.Combine(mediaDirectory, usable[0]);
                var contentType = DlnaMimeCatalog.TryGetByFileExtension(Path.GetExtension(fullPath), out var mime)
                    ? mime.ToMimeString()
                    : "text/plain";

                return PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath));
            }

            // ponytail: zipped in memory - subtitle files are a few tens of KB each; stream to a temp file if a
            // library ever links dozens of large ones to one file.
            using var buffer = new MemoryStream();

            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var relativePath in usable)
                {
                    zip.CreateEntryFromFile(Path.Combine(mediaDirectory, relativePath), relativePath, CompressionLevel.Optimal);
                }
            }

            var zipName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.subtitles.zip";

            return File(buffer.ToArray(), "application/zip", zipName);
        }
    }
}
