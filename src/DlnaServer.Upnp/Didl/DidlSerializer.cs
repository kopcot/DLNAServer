using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// Serialises a DIDL-Lite document to the compact form carried inside a SOAP Browse response.
    /// </summary>
    /// <remarks>
    /// The result is embedded as escaped text inside the <c>Result</c> element, so it must carry no XML
    /// declaration and no indentation. The serializer is cached: constructing an
    /// <see cref="XmlSerializer"/> generates and compiles an assembly, which is far too expensive to
    /// repeat per Browse call.
    /// </remarks>
    public static class DidlSerializer
    {
        private static readonly XmlSerializer _serializer = new(typeof(DidlDocument));

        private static readonly XmlWriterSettings _settings = new()
        {
            Indent = false,
            OmitXmlDeclaration = true,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            Encoding = Encoding.UTF8,
        };

        /// <summary>
        /// Bytes of buffer reserved per object in the document, before the writer starts growing.
        /// </summary>
        /// <remarks>
        /// Measured against real responses rather than guessed: a DIDL item with a thumbnail, a duration,
        /// a resolution and the two codec elements runs to roughly 1.2 KB, and a container to a third of
        /// that. <see cref="StringBuilder"/> grows by doubling and copies everything it holds each time,
        /// so a 32-item response starting from the default 16 characters reallocates and copies about
        /// twelve times, the last few of those on the large object heap. Reserving up front pays one
        /// allocation instead, and an over-estimate costs only the slack in a buffer that is released as
        /// soon as the response is written.
        /// </remarks>
        private const int ReservedBytesPerObject = 1_536;

        /// <summary>
        /// Floor for the reservation, so an empty or single-item document does not allocate a large
        /// buffer to hold a few hundred bytes.
        /// </summary>
        private const int MinimumReservedBytes = 1_024;

        public static string Serialize(DidlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var objectCount = document.Containers.Length + document.Items.Length;
            var reserved = Math.Max(MinimumReservedBytes, objectCount * ReservedBytesPerObject);

            using var text = new StringWriter(new StringBuilder(reserved));

            using (var writer = XmlWriter.Create(text, _settings))
            {
                _serializer.Serialize(writer, document, document.Namespaces);
            }

            return text.ToString();
        }
    }
}
