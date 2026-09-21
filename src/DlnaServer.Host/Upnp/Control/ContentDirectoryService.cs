using DlnaServer.Host.Indexing;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Core.Delivery;
using System.Diagnostics;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Persistence.Repositories;
using DlnaServer.Upnp.Didl;
using DlnaServer.Upnp.Soap.ContentDirectory;
using DlnaServer.Upnp.Ssdp;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// Implements the ContentDirectory service: how a renderer lists and navigates the library.
    /// </summary>
    /// <remarks>
    /// Reads only. Metadata and thumbnails are produced by the background processing service, never
    /// inside a Browse call - the reference did that inline and its own logs show it costing 23 ms on
    /// average for slow requests, before any ffmpeg work is counted.
    /// </remarks>
    internal sealed partial class ContentDirectoryService : IContentDirectoryService
    {
        private readonly IMediaDirectoryRepository _directories;
        private readonly IMediaFileRepository _files;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IUpnpDeviceRegistry _devices;
        private readonly IMediaCacheBacklog _backlog;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<ContentDirectoryService> _logger;

        public ContentDirectoryService(
            IMediaDirectoryRepository directories,
            IMediaFileRepository files,
            IHttpContextAccessor httpContext,
            IUpnpDeviceRegistry devices,
            IMediaCacheBacklog backlog,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<ContentDirectoryService> logger)
        {
            _directories = directories;
            _files = files;
            _httpContext = httpContext;
            _devices = devices;
            _backlog = backlog;
            _options = options;
            _logger = logger;
        }

        public async Task<BrowseResponse> Browse(
            string ObjectID,
            string BrowseFlag,
            string Filter,
            int StartingIndex,
            int RequestedCount,
            string SortCriteria)
        {
            var stopwatch = Stopwatch.StartNew();
            var options = BrowseRequest.Parse(
                BrowseFlag,
                Filter,
                StartingIndex,
                RequestedCount,
                SortCriteria,
                _options.CurrentValue.Compatibility.MaxBrowseRequestedCount);

            var endpoint = ResolveEndpoint();

            // The SOAP contract carries no token, but the request does: a renderer that walks away
            // mid-Browse should not leave the counts and both page queries running against SQLite.
            var response = await BrowseAsync(
                ObjectID,
                options,
                endpoint,
                _httpContext.HttpContext?.RequestAborted ?? CancellationToken.None);

            stopwatch.Stop();

            var remoteAddress = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();

            LogBrowseCompleted(
                remoteAddress,
                ObjectID ?? "0",
                BrowseFlag ?? "BrowseDirectChildren",
                StartingIndex,
                RequestedCount,
                response.NumberReturned,
                response.TotalMatches,
                stopwatch.Elapsed.TotalMilliseconds);

            LogBrowseFilter(remoteAddress, Filter ?? "*");

            return response;
        }

        public GetSystemUpdateIdResponse GetSystemUpdateID()
        {
            return new GetSystemUpdateIdResponse { Id = 1 };
        }

        public GetSearchCapabilitiesResponse GetSearchCapabilities()
        {
            return new GetSearchCapabilitiesResponse();
        }

        public GetSortCapabilitiesResponse GetSortCapabilities()
        {
            return new GetSortCapabilitiesResponse();
        }

        public IsAuthorizedResponse IsAuthorized(string DeviceID)
        {
            return new IsAuthorizedResponse();
        }

        public XGetFeatureListResponse X_GetFeatureList()
        {
            return new XGetFeatureListResponse();
        }

        public XSetBookmarkResponse X_SetBookmark(int CategoryType, int RID, string ObjectID, int PosSecond)
        {
            LogBookmarkDiscarded(ObjectID ?? string.Empty, PosSecond);

            return new XSetBookmarkResponse();
        }

        private async Task<BrowseResponse> BrowseAsync(
            string objectId,
            BrowseRequest options,
            string endpoint,
            CancellationToken cancellationToken)
        {
            var isRoot = !Guid.TryParse(objectId, out var publicId);

            if (options.BrowseMetadata)
            {
                return await BrowseMetadataAsync(isRoot, publicId, endpoint, cancellationToken);
            }

            List<MediaDirectoryDto> pagedContainers;
            List<MediaFileDto> pagedFiles;
            uint totalMatches;

            if (isRoot)
            {
                // The root is bounded by construction - one container per configured source folder and at
                // most RecentlyAddedCount files - so it is read whole and paged in memory.
                var roots = await _directories.GetSourceRootsAsync(cancellationToken);

                // The reference surfaces the most recently indexed files directly in the root, so a
                // renderer sees new content without walking the tree. Kept.
                // Recently added files should not be sorted out differently as by by descending by created date-time
                var recentCount = _options.CurrentValue.Library.RecentlyAddedCount;
                var recentlyAdded = recentCount > 0
                    ? await _files.GetRecentlyAddedAsync(recentCount, cancellationToken)
                    : [];

                var (containers, files) = ComposeRoot(roots, recentlyAdded, options);

                totalMatches = (uint)(containers.Count + files.Count);
                (pagedContainers, pagedFiles) = Paginate(containers, files, options);
            }
            else
            {
                // A folder is unbounded, so its page is cut by the database. Reading the whole folder to
                // return at most MaxBrowseRequestedCount cost a 5,000-file directory 5,000 DTOs per
                // request, sorted twice, with the backing array crossing the large object heap.
                var containerCount = await _directories.CountChildrenAsync(
                    publicId,
                    cancellationToken);
                var fileCount = await _files.CountByDirectoryAsync(
                    publicId,
                    cancellationToken);

                totalMatches = (uint)(containerCount + fileCount);

                // Containers come first and files continue the same list, so the requested window has to
                // be split across the boundary exactly as the in-memory Paginate did.
                var containerSkip = Math.Min(options.StartingIndex, containerCount);
                var containerTake = Math.Min(options.RequestedCount, containerCount - containerSkip);
                var fileSkip = Math.Max(options.StartingIndex - containerCount, 0);
                var fileTake = options.RequestedCount - containerTake;

                pagedContainers =
                [
                    .. await _directories.GetChildrenPageAsync(
                        publicId,
                        containerSkip,
                        containerTake,
                        options.SortByDate,
                        options.SortDescending,
                        cancellationToken),
                ];

                pagedFiles =
                [
                    .. await _files.GetByDirectoryPageAsync(
                        publicId,
                        fileSkip,
                        fileTake,
                        options.SortByDate,
                        options.SortDescending,
                        cancellationToken),
                ];
            }

            // Every child of a BrowseDirectChildren listing declares the browsed container as its parent,
            // which for the root means "0" even for the recently-added files that live deeper in the tree.
            //
            // The PARSED identifier, not the string the renderer sent. Guid.TryParse trims surrounding
            // whitespace, and \v and \f count as whitespace to it while being illegal in XML 1.0 - so an
            // ObjectID of "\v<a real guid>" parsed happily and then went out verbatim in parentID, where
            // it tore the response during the body write. Every other field goes through ToXmlText; this
            // was the one carrying raw request input, and echoing the canonical form is simpler than
            // escaping it.
            var listingParentId = isRoot ? DidlMapper.RootObjectId : publicId.ToString();

            var document = new DidlDocument
            {
                Containers = pagedContainers
                    .Select(c => DidlMapper.MapContainer(c, endpoint, listingParentId))
                    .ToArray(),
                Items = pagedFiles
                    .Select(f => DidlMapper.MapItem(f, endpoint, listingParentId))
                    .ToArray(),
            };

            ApplyFilter(document, options);

            WarmPreviews(pagedFiles, _options.CurrentValue, _backlog);

            return new BrowseResponse
            {
                Didl = document,
                NumberReturned = (uint)(document.Containers.Length + document.Items.Length),
                TotalMatches = totalMatches,
                UpdateID = 1,
            };
        }

        /// <summary>
        /// Describes one object rather than listing children.
        /// </summary>
        /// <remarks>
        /// The reference ignored <c>BrowseFlag</c> entirely and returned a child listing here, which
        /// breaks per-item metadata refresh and resume prompts on renderers that rely on it.
        /// </remarks>
        private async Task<BrowseResponse> BrowseMetadataAsync(
            bool isRoot,
            Guid publicId,
            string endpoint,
            CancellationToken cancellationToken)
        {
            var document = new DidlDocument();

            if (isRoot)
            {
                document.Containers =
                [
                    new DidlContainer
                    {
                        ObjectId = DidlMapper.RootObjectId,
                        ParentId = "-1",
                        Class = DlnaItemClass.Container.ToUpnpClass(),
                        Title = _options.CurrentValue.Server.FriendlyName,
                        Searchable = "1",
                    },
                ];
            }
            else if (await _directories.GetByPublicIdAsync(publicId, cancellationToken) is { } directory)
            {
                // Describing the object itself, so it declares the container it really belongs to.
                document.Containers =
                    [DidlMapper.MapContainer(directory, endpoint, DidlMapper.ParentIdOf(directory))];
            }
            else if (await _files.GetByPublicIdAsync(publicId, cancellationToken) is { } file)
            {
                document.Items = [DidlMapper.MapItem(file, endpoint, DidlMapper.ParentIdOf(file))];
            }

            var count = (uint)(document.Containers.Length + document.Items.Length);

            return new BrowseResponse
            {
                Didl = document,
                NumberReturned = count,
                TotalMatches = count,
                UpdateID = 1,
            };
        }

        /// <summary>
        /// The root listing: the source folders in the order the renderer asked for, followed by the
        /// recently added files in the order the database returned them.
        /// </summary>
        /// <remarks>
        /// Internal rather than private so it can be tested directly, exactly as <see cref="Paginate"/>
        /// is - and here for a sharper reason. The ordering is carried by the ORDER OF TWO STATEMENTS:
        /// the sort runs while the file list is still empty, and the recently added files are appended
        /// after it. Appending them first would silently sort them by title and throw the indexing order
        /// away, which is a customer-reported defect, and nothing about it would fail to compile.
        /// <para>
        /// Recently added therefore stays in the order <c>GetRecentlyAddedAsync</c> produced - newest
        /// indexed first - whatever <c>SortCriteria</c> asks for. That is deliberate: the list means
        /// nothing in any other order.
        /// </para>
        /// </remarks>
        internal static (List<MediaDirectoryDto> Containers, List<MediaFileDto> Files) ComposeRoot(
            IReadOnlyList<MediaDirectoryDto> roots,
            IReadOnlyList<MediaFileDto> recentlyAdded,
            BrowseRequest options)
        {
            var containers = new List<MediaDirectoryDto>(roots);
            var files = new List<MediaFileDto>();

            Sort(containers, files, options);

            files.AddRange(recentlyAdded);

            return (containers, files);
        }

        private static void Sort(
            List<MediaDirectoryDto> containers,
            List<MediaFileDto> files,
            BrowseRequest options)
        {
            if (options.SortByDate)
            {
                containers.Sort(static (a, b) => a.CreatedUtc.CompareTo(b.CreatedUtc));
                files.Sort(static (a, b) => a.FileCreatedUtc.CompareTo(b.FileCreatedUtc));
            }
            else
            {
                containers.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
                files.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title));
            }

            if (options.SortDescending)
            {
                containers.Reverse();
                files.Reverse();
            }
        }

        /// <summary>
        /// Slices containers and items as one logical list, containers first.
        /// </summary>
        /// <remarks>
        /// Internal rather than private so it can be tested directly. It is pure, it is the one place an
        /// out-of-range page index is handled, and reaching it through <c>BrowseAsync</c> would need the
        /// whole service stood up - which is why the off-by-one that threw out of every Browse on a
        /// fresh deployment had no test to catch it.
        /// </remarks>
        internal static (List<MediaDirectoryDto> Containers, List<MediaFileDto> Files) Paginate(
            List<MediaDirectoryDto> containers,
            List<MediaFileDto> files,
            BrowseRequest options)
        {
            var containerStart = Math.Min(options.StartingIndex, containers.Count);
            var containerCount = Math.Min(options.RequestedCount, containers.Count - containerStart);
            var pagedContainers = containers.GetRange(containerStart, containerCount);

            var remaining = options.RequestedCount - containerCount;

            if (remaining <= 0)
            {
                return (pagedContainers, []);
            }

            // Clamped at BOTH ends. List.GetRange refuses index > Count even when count is 0 - its
            // precondition is `Count - index < count` - so clamping only the count is not enough: a
            // renderer paging past the end threw ArgumentException out of Browse, mid-response. A fresh
            // deployment reaches it immediately, because RecentlyAddedCount of 0 leaves files empty.
            var fileStart = Math.Clamp(options.StartingIndex - containers.Count, 0, files.Count);
            var fileCount = Math.Min(remaining, files.Count - fileStart);

            return (pagedContainers, files.GetRange(fileStart, fileCount));
        }

        /// <summary>
        /// Queues the previews of the objects just returned, so they are in memory before the television
        /// asks for them.
        /// </summary>
        /// <remarks>
        /// A renderer that opens a folder issues one thumbnail request per tile within milliseconds of
        /// parsing the reply, and each one that misses the cache is a separate read. Browse is the only
        /// moment the server knows which thirty are about to be wanted, and the backlog and its drain
        /// service already existed - they were simply never fed from here, only from a media file being
        /// served, which is far too late to help the folder it lives in.
        /// <para>
        /// The path is derived rather than read. That keeps this to no database work at all on the Browse
        /// path, and a derivation that is ever wrong costs nothing: the drain finds no file, logs a
        /// deferral, and the ordinary request serves it from wherever it really is.
        /// </para>
        /// <para>
        /// Enqueueing is non-blocking and bounded - the channel drops the request when it is full, and
        /// the backlog refuses a path already queued - so a television paging quickly through a large
        /// folder cannot build a queue behind itself.
        /// </para>
        /// <para>
        /// <see cref="FileCacheOptions.WarmPreviewsOnBrowse"/> turns the whole pass off, for a deployment
        /// that would rather not spend disc reads on previews before they are asked for.
        /// </para>
        /// </remarks>
        internal static void WarmPreviews(
            IReadOnlyList<MediaFileDto> files,
            DlnaOptions options,
            IMediaCacheBacklog backlog)
        {
            if (!options.FileCache.WarmPreviewsOnBrowse)
            {
                return;
            }

            foreach (var file in files)
            {
                if (file.ThumbnailPublicId is not { } thumbnailPublicId)
                {
                    continue;
                }

                // The preview's own identifier, not the media file's: the drain reads the image the
                // database holds by it, and only falls back to the file beside the media.
                _ = backlog.TryEnqueue(new MediaCacheRequest(
                    thumbnailPublicId,
                    ThumbnailPath.Resolve(file, options, ThumbnailPath.DefaultExtension),
                    CachedContentClass.Thumbnail));
            }
        }

        /// <summary>
        /// Drops optional properties the caller did not ask for.
        /// </summary>
        /// <remarks>
        /// The required identifiers, class and title are always emitted; a response without them is not
        /// usable. Only the extras a filter can legitimately exclude are removed.
        /// </remarks>
        private static void ApplyFilter(DidlDocument document, BrowseRequest options)
        {
            // Hoisted: these three are loop-invariant - BrowseRequest is not mutated here and Includes is
            // a pure query - so evaluating them per DIDL object meant 3N filter searches on the Browse
            // hot path where 3 do.
            if (options.IncludesAllProperties)
            {
                return;
            }

            var wantsAlbumArt = options.Includes("upnp:albumArtURI");
            var wantsIcon = options.Includes("upnp:icon");
            var wantsDate = options.Includes("dc:date");

            foreach (var item in document.Items)
            {
                if (!wantsAlbumArt)
                {
                    item.AlbumArtUri = null;
                }

                if (!wantsIcon)
                {
                    item.Icon = null;
                }

                if (!wantsDate)
                {
                    item.Date = null;
                }
            }

            foreach (var container in document.Containers)
            {
                if (!wantsAlbumArt)
                {
                    container.AlbumArtUri = null;
                }

                if (!wantsIcon)
                {
                    container.Icon = null;
                }
            }
        }

        /// <summary>
        /// The endpoint that resource URLs are built against.
        /// </summary>
        /// <remarks>
        /// Resolved through the device registry rather than taken straight from the connection, for two
        /// reasons. It guarantees the URL uses an address the server actually advertised over SSDP, so a
        /// renderer is never handed one it cannot reach. And it avoids emitting a raw IPv6 address, which
        /// would need bracketing to be a valid URL - a request arriving on the IPv6 loopback otherwise
        /// produces <c>http://::1:26852/...</c>, which no client can parse.
        /// </remarks>
        private string ResolveEndpoint()
        {
            var connection = _httpContext.HttpContext?.Connection;
            var port = connection?.LocalPort ?? _options.CurrentValue.Server.Port;

            try
            {
                var identity = _devices.Resolve(connection?.LocalIpAddress);

                return $"{identity.Address}:{port}";
            }
            catch (InvalidOperationException)
            {
                // No advertised interface - only reachable over loopback, so say so plainly.
                return $"127.0.0.1:{port}";
            }
        }
    }
}
