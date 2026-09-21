using System.Globalization;
using System.Text;

namespace DlnaServer.Upnp.Ssdp
{
    /// <summary>
    /// Builds the <c>SERVER</c> header sent in SSDP messages and UPnP responses.
    /// </summary>
    /// <remarks>
    /// Format is reproduced from the reference exactly:
    /// <c>{platform}/{bits}bit/{osMajor}.{osMinor} UPnP/1.0 DLNADOC/1.5 zen_dlna/{major}.{minor}/{port}</c>,
    /// for example <c>Linux/64bit/5.10 UPnP/1.0 DLNADOC/1.5 zen_dlna/1.0/26851</c>.
    /// Some renderers key device-specific behaviour off this string, so its shape is part of the contract.
    /// </remarks>
    public static class UpnpServerSignature
    {
        /// <summary>
        /// Builds the signature for the current machine.
        /// </summary>
        public static string Create(Version serverVersion, int port)
        {
            ArgumentNullException.ThrowIfNull(serverVersion);

            return Create(
                Environment.OSVersion.Platform,
                Environment.OSVersion.Version,
                IntPtr.Size * 8,
                serverVersion,
                port);
        }

        /// <summary>
        /// Builds the signature from explicit values, so the format can be asserted without depending
        /// on the machine the test runs on.
        /// </summary>
        public static string Create(
            PlatformID platform,
            Version osVersion,
            int bitness,
            Version serverVersion,
            int port)
        {
            ArgumentNullException.ThrowIfNull(osVersion);
            ArgumentNullException.ThrowIfNull(serverVersion);

            var builder = new StringBuilder(128);

            _ = builder
                .Append(ToPlatformName(platform)).Append('/')
                .Append(bitness.ToString(CultureInfo.InvariantCulture)).Append("bit/")
                .Append(osVersion.Major.ToString(CultureInfo.InvariantCulture)).Append('.')
                .Append(osVersion.Minor.ToString(CultureInfo.InvariantCulture))
                .Append(" UPnP/1.0 DLNADOC/1.5 zen_dlna/")
                .Append(serverVersion.Major.ToString(CultureInfo.InvariantCulture)).Append('.')
                .Append(serverVersion.Minor.ToString(CultureInfo.InvariantCulture)).Append('/')
                .Append(port.ToString(CultureInfo.InvariantCulture));

            return builder.ToString();
        }

        private static string ToPlatformName(PlatformID platform)
        {
            return platform switch
            {
                PlatformID.Win32NT or PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.WinCE => "WIN",
                PlatformID.Unix => "Linux",
                _ => platform.ToString(),
            };
        }
    }
}
