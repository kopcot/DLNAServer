namespace DlnaServer.Host.Upnp.Discovery
{
    internal sealed partial class SsdpNotifierHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Advertising on {Address} as device {DeviceId}")]
        private partial void LogAdvertising(string address, Guid deviceId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "No usable network interface was found, so nothing is being advertised yet. The "
                + "interfaces are re-read every announce interval, so a lease that arrives late is picked "
                + "up on its own - this is not permanent.")]
        private partial void LogNoInterfaces();

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Information,
            Message = "Network interfaces changed; now advertising on {InterfaceCount}. A DHCP lease that "
                + "changes leaves the previous address unbindable, which used to silence announcements "
                + "for the life of the process.")]
        private partial void LogInterfacesChanged(int interfaceCount);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "SSDP announcement from {Address} failed ({Reason})")]
        private partial void LogSendFailed(string address, string reason);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Could not open an SSDP socket on {Address} ({Reason})")]
        private partial void LogSocketFailed(string address, string reason);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "The shutdown byebye announcement failed; renderers will drop the server when "
                + "its advertised max-age expires")]
        private partial void LogByebyeFailed(Exception exception);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "An SSDP alive announcement failed; the next one runs after the usual interval")]
        private partial void LogAnnounceFailed(Exception exception);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "The announce interval could not be read because the configuration is invalid "
                + "({Reason}); keeping the last good value of {IntervalSeconds}s so the server stays "
                + "discoverable while it is fixed")]
        private partial void LogIntervalUnavailable(string reason, double intervalSeconds);
    }
}
