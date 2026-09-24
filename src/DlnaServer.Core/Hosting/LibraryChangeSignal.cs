namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="ILibraryChangeSignal"/>
    public sealed class LibraryChangeSignal : ILibraryChangeSignal
    {
        private long _generation;

        public long Generation => Interlocked.Read(ref _generation);

        public void MarkChanged()
        {
            _ = Interlocked.Increment(ref _generation);
        }
    }
}
