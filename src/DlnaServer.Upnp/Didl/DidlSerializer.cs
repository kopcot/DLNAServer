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

        /// <remarks>
        /// No capacity is reserved for the output, and that is deliberate. On .NET Core a
        /// <see cref="StringBuilder"/> grows by linking new chunks of at most 8,000 characters rather than
        /// doubling and copying one buffer, so the default builder never allocates on the large object heap
        /// however long the document is. An up-front reservation sized per object did the opposite: 32 objects
        /// asked for one 49,152-character buffer - a 98 KB array on the large object heap - on every full
        /// Browse page, which on a 2-4 GB NAS is exactly the churn section 3 of docs/decisions.md rules out.
        /// </remarks>
        public static string Serialize(DidlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            using var text = new StringWriter();

            using (var writer = XmlWriter.Create(text, _settings))
            {
                _serializer.Serialize(writer, document, document.Namespaces);
            }

            return text.ToString();
        }
    }
}
