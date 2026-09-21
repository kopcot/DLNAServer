namespace DlnaServer.Core.Dlna
{
    /// <summary>
    /// Coarse classification of a MIME type, used to pick a media processor and a default UPnP class.
    /// </summary>
    public enum DlnaMedia
    {
        Unknown = 0,
        Container = 1,
        Video = 2,
        Image = 3,
        Audio = 4,
        Subtitle = 5,
    }
}
