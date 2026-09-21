namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Where one response's bytes are coming from.
    /// </summary>
    /// <remarks>
    /// A readonly struct: this is returned on every media request, including one per byte range during a
    /// playback, so it must not allocate.
    /// </remarks>
    public readonly struct MediaContentSource
    {
        private MediaContentSource(bool isCached, ReadOnlyMemory<byte> content)
        {
            IsCached = isCached;
            Content = content;
        }

        /// <summary>
        /// True when <see cref="Content"/> holds the bytes and the disc is not touched.
        /// </summary>
        public bool IsCached { get; }

        /// <summary>
        /// The cached payload, empty unless <see cref="IsCached"/>.
        /// </summary>
        public ReadOnlyMemory<byte> Content { get; }

        public static MediaContentSource FromCache(ReadOnlyMemory<byte> content)
        {
            return new MediaContentSource(isCached: true, content);
        }

        public static MediaContentSource FromDisc()
        {
            return new MediaContentSource(isCached: false, ReadOnlyMemory<byte>.Empty);
        }
    }
}
