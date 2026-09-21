using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>X_SetBookmark</c>, a Samsung extension to ContentDirectory. Returns no arguments.
    /// </summary>
    /// <remarks>
    /// A television reports a resume position through this action. Accepted and discarded: nothing here
    /// stores playback state, and a fault would make a TV treat the whole service as broken.
    /// </remarks>
    [MessageContract(WrapperName = "X_SetBookmarkResponse")]
    [XmlRoot(ElementName = "X_SetBookmarkResponse")]
    public sealed class XSetBookmarkResponse
    {
    }
}
