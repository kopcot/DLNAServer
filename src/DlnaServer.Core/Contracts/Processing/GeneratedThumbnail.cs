using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts.Processing
{
    /// <summary>
    /// A thumbnail that has been written to the cache directory.
    /// </summary>
    /// <param name="FilePath">Absolute path of the generated image.</param>
    /// <param name="Mime">Format the image was encoded in.</param>
    /// <param name="Width">Pixel width.</param>
    /// <param name="Height">Pixel height.</param>
    /// <param name="SizeInBytes">Size of the written file.</param>
    /// <param name="Content">
    /// The image bytes, only when database storage is enabled. Null otherwise, so a generation pass over
    /// a large library does not carry every image in memory.
    /// </param>
    /// <param name="WasAdopted">
    /// True when the image was already on disk and taken as-is rather than produced now. Without this the
    /// caller cannot tell the two apart, and a first pass over a library the reference has been previewing
    /// for years reports having created every thumbnail in it.
    /// </param>
    public sealed record GeneratedThumbnail(
        string FilePath,
        DlnaMime Mime,
        int Width,
        int Height,
        long SizeInBytes,
        byte[]? Content,
        bool WasAdopted);
}
