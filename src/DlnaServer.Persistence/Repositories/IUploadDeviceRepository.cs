using DlnaServer.Core.Contracts;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// Remembers where each browser last sent an upload.
    /// </summary>
    public interface IUploadDeviceRepository
    {
        /// <summary>
        /// The folder this device last uploaded into, or null if it has not uploaded before.
        /// </summary>
        Task<string?> GetLastDestinationAsync(
            string fingerprint,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records an upload against the device, adding the row on its first one.
        /// </summary>
        Task RecordAsync(
            UploadDeviceUpdateDto device,
            CancellationToken cancellationToken = default);
    }
}
