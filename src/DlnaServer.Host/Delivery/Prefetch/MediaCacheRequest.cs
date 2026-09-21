using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Delivery.Prefetch
{
    /// <summary>
    /// A request to read one payload into the served-bytes cache, behind the response that triggered it.
    /// </summary>
    /// <param name="PublicId">
    /// Identifies the payload, and what it identifies follows from <paramref name="ContentClass"/>: for
    /// <see cref="CachedContentClass.Media"/> the media file, so a failed read can be recorded against it
    /// and not attempted again; for <see cref="CachedContentClass.Thumbnail"/> the thumbnail row, so the
    /// image stored in the database can be read from there rather than off the platter.
    /// </param>
    /// <param name="FilePath">Absolute path of the file to read.</param>
    /// <param name="ContentClass">
    /// What is being cached, which decides how long it is kept and - load-bearing - whether a failed read
    /// is recorded against the file. A preview that cannot be read is simply not there yet, so marking
    /// its media file as too large to cache would condemn the film for a missing thumbnail.
    /// </param>
    public readonly record struct MediaCacheRequest(
        Guid PublicId,
        string FilePath,
        CachedContentClass ContentClass = CachedContentClass.Media);
}
