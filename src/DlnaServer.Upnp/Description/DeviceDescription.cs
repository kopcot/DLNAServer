namespace DlnaServer.Upnp.Description
{
    /// <summary>
    /// The identity fields that vary between one server and another in <c>description.xml</c>.
    /// </summary>
    /// <param name="DeviceId">
    /// UUID advertised as the device's <c>UDN</c> and in every SSDP <c>USN</c>.
    /// </param>
    /// <param name="FriendlyName">Name a renderer shows in its device list.</param>
    /// <param name="ManufacturerName">Free-text manufacturer.</param>
    /// <param name="ManufacturerUrl">Manufacturer link, often a mailto.</param>
    /// <param name="ModelName">Model string, used by some renderers to key quirks.</param>
    public sealed record DeviceDescription(
        Guid DeviceId,
        string FriendlyName,
        string ManufacturerName,
        string ManufacturerUrl,
        string ModelName);
}
