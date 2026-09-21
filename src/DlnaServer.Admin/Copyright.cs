using System.Globalization;

namespace DlnaServer.Admin
{
    /// <summary>
    /// The copyright line, in one place because two components show it.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> taken from <c>ServerOptions.ManufacturerName</c>, which looks like the same
    /// fact and is not: that string is the identity the device advertises to renderers, an operator is free
    /// to change it, and some televisions key their own quirks off it. Who holds the copyright in the
    /// software does not change when a deployment renames its device.
    /// </remarks>
    internal static class Copyright
    {
        private const string Holder = "Kopco";

        /// <remarks>
        /// The year is read once, when the type is first touched, so a server left running across New Year
        /// shows the old year until it restarts. Stale by a few days beats a notice that has to be edited
        /// every January and will not be.
        /// </remarks>
        internal static readonly string Notice = string.Create(
            CultureInfo.InvariantCulture,
            $"© {DateTime.Now.Year} {Holder}");
    }
}
