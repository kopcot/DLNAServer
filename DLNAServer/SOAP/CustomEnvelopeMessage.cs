using SoapCore;
using System.ServiceModel.Channels;
using System.Xml;

namespace DLNAServer.SOAP
{
    public class CustomEnvelopeMessage : CustomMessage
    {
        public CustomEnvelopeMessage() : base()
        {
        }
        public CustomEnvelopeMessage(Message message) : base(message: message)
        {
        }
        /// <summary>
        /// Added additional attributes to <see cref="CustomMessage"/> <br />
        /// encodingStyle
        /// </summary>
        /// <param name="writer"></param>
        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            if (StandAloneAttribute.HasValue)
            {
                writer.WriteStartDocument(StandAloneAttribute.Value);
            }
            else
            {
                writer.WriteStartDocument();
            }

            string prefix = string.Intern(Version.Envelope.NamespacePrefix(NamespaceManager));
            string envelopeNamespace = string.Intern(Version.Envelope.Namespace());

            const string envelope = "Envelope";
            const string encodingStyle = "encodingStyle";
            const string encodingStyleUri = "http://schemas.xmlsoap.org/soap/encoding/";
            writer.WriteStartElement(prefix, envelope, envelopeNamespace);
            writer.WriteXmlnsAttribute(prefix, envelopeNamespace);
            writer.WriteAttributeString(prefix, encodingStyle, null, encodingStyleUri);

            const string xsd = "xsd";
            string xsdPrefix = string.Intern(Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, xsd, Namespaces.XMLNS_XSD));
            writer.WriteXmlnsAttribute(xsdPrefix, Namespaces.XMLNS_XSD);

            const string xsi = "xsi";
            string xsiPrefix = string.Intern(Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, xsi, Namespaces.XMLNS_XSI));
            writer.WriteXmlnsAttribute(xsiPrefix, Namespaces.XMLNS_XSI);

            if (AdditionalEnvelopeXmlnsAttributes != null)
            {
                foreach (var rec in AdditionalEnvelopeXmlnsAttributes)
                {
                    writer.WriteXmlnsAttribute(string.Intern(rec.Key), string.Intern(rec.Value));
                }
            }
        }
    }
}
