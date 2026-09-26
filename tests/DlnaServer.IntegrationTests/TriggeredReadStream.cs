namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// A read-only request body that runs an action once reading has passed a given offset.
    /// </summary>
    /// <remarks>
    /// Lets a test act in the middle of an upload - put a file in the way, or drop the connection by
    /// throwing - at a point the controller has already committed to, which a fully buffered body cannot.
    /// Each read returns at most <see cref="ChunkSize"/> bytes so the offset is reached in the middle of a
    /// part rather than in the first read.
    /// </remarks>
    internal sealed class TriggeredReadStream : Stream
    {
        private const int ChunkSize = 4 * 1024;

        private readonly MemoryStream _inner;
        private readonly long _triggerAt;
        private readonly Action _onTrigger;
        private bool _hasTriggered;

        public TriggeredReadStream(byte[] content, long triggerAt, Action onTrigger)
        {
            _inner = new MemoryStream(content, writable: false);
            _triggerAt = triggerAt;
            _onTrigger = onTrigger;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (!_hasTriggered && _inner.Position >= _triggerAt)
            {
                _hasTriggered = true;
                _onTrigger();
            }

            return _inner.Read(buffer[..Math.Min(buffer.Length, ChunkSize)]);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(Read(buffer.AsSpan(offset, count)));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
