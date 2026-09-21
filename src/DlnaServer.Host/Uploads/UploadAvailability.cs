using DlnaServer.Core.Uploads;

namespace DlnaServer.Host.Uploads
{
    /// <inheritdoc cref="IUploadAvailability"/>
    internal sealed class UploadAvailability : IUploadAvailability
    {
        public UploadAvailability(bool isAcceptingUploads)
        {
            IsAcceptingUploads = isAcceptingUploads;
        }

        public bool IsAcceptingUploads { get; }
    }
}
