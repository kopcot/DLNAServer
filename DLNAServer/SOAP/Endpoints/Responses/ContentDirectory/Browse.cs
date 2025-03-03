using DLNAServer.SOAP.Constants;
using System.ServiceModel;
using System.Xml;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ContentDirectory
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "BrowseResponse")]
    [XmlRoot(ElementName = "BrowseResponse")]
    public class Browse
    {
        /// <summary>
        /// Browse items for application
        /// </summary>
        [XmlIgnore]
        public Result Result { get; set; } = new();
        /// <summary>
        /// Serialized browse items for SOAP request 
        /// </summary>
        [XmlElement(ElementName = "Result")]
        public string ResultOutput { get => GetResultOutput(); set { } }  // This setter is a no-op, included only to make the property compatible with XML serialization.
        [XmlElement(ElementName = "NumberReturned")]
        public uint NumberReturned { get; set; }
        [XmlElement(ElementName = "TotalMatches")]
        public uint TotalMatches { get; set; }
        [XmlElement(ElementName = "UpdateID")]
        public uint UpdateID { get; set; }

        private static readonly XmlSerializer XmlSerializer = new(typeof(DidlLite));
        private string GetResultOutput()
        {
            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true, NamespaceHandling = NamespaceHandling.OmitDuplicates }))
                {
                    XmlSerializer.Serialize(xmlWriter, Result.DidlLite);
                    return stringWriter.ToString();
                }
            }
        }
    }
    [XmlRoot(ElementName = "Result")]
    public class Result
    {
        [XmlElement("DIDL-Lite")]
        public DidlLite DidlLite { get; set; } = new DidlLite();
    }
    [XmlRoot(ElementName = "DIDL-Lite", Namespace = XmlNamespaces.NS_DIDL)]
    public class DidlLite
    {
        [XmlNamespaceDeclarations]
        public XmlSerializerNamespaces xmlns = new(
            [
                new ("dc", XmlNamespaces.NS_DC),
                new ("dlna", XmlNamespaces.NS_DLNA),
                new ("upnp", XmlNamespaces.NS_UPNP),
                new ("sec", XmlNamespaces.NS_SEC),
                new (null, XmlNamespaces.NS_DIDL)
            ]);
        [XmlElement("container")]
        public BrowseItem[] Containers { get; set; } = [];

        [XmlElement("item")]
        public BrowseItem[] BrowseItems { get; set; } = [];
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
