using System.ServiceModel;
using System.Xml.Serialization;
using DlnaServer.Upnp.Didl;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to a <c>Browse</c> action.
    /// </summary>
    [MessageContract(WrapperName = "BrowseResponse")]
    [XmlRoot(ElementName = "BrowseResponse")]
    public sealed class BrowseResponse
    {
        // Serialised once per process rather than per response: every Browse assigns Didl, so a
        // per-instance initializer serialised an empty document only to throw the result away.
        private static readonly string _emptyResult = DidlSerializer.Serialize(new DidlDocument());

        private DidlDocument _didl = new();
        private string _result = _emptyResult;

        /// <summary>
        /// The document that will be serialised into <see cref="Result"/>.
        /// </summary>
        /// <remarks>
        /// <b>Assign this once the document is complete.</b> Serialising here rather than when
        /// <see cref="Result"/> is read is what keeps a failure recoverable: the XML serializer reads
        /// that property while writing the response body, so anything thrown from it arrives after the
        /// status line and the first bytes have gone - the connection tears and the renderer gets no
        /// SOAP fault, on the hottest path in the service. Doing it on assignment puts the failure back
        /// inside the service call, where it can still become a fault.
        /// </remarks>
        [XmlIgnore]
        public DidlDocument Didl
        {
            get => _didl;
            set
            {
                _didl = value;
                _result = DidlSerializer.Serialize(value);
            }
        }

        /// <summary>
        /// <b>Result</b><br />
        /// The DIDL-Lite document, carried as escaped XML inside this element.
        /// </summary>
        /// <remarks>
        /// Produced from <see cref="Didl"/> when that is assigned, so callers only ever build the object
        /// model. The setter exists because XML serialization requires one; nothing should call it.
        /// </remarks>
        [XmlElement(ElementName = "Result")]
        public string Result
        {
            get => _result;
            set => throw new NotSupportedException("Result is produced from the DIDL-Lite document.");
        }

        /// <summary>
        /// <b>NumberReturned</b><br />
        /// How many objects this response carries.
        /// </summary>
        [XmlElement(ElementName = "NumberReturned")]
        public uint NumberReturned { get; set; }

        /// <summary>
        /// <b>TotalMatches</b><br />
        /// How many exist in total, so the renderer knows whether to ask for another page.
        /// </summary>
        [XmlElement(ElementName = "TotalMatches")]
        public uint TotalMatches { get; set; }

        /// <summary>
        /// <b>UpdateID</b><br />
        /// Changes when the library changes, letting a renderer discard a cached listing.
        /// </summary>
        [XmlElement(ElementName = "UpdateID")]
        public uint UpdateID { get; set; }
    }
}
