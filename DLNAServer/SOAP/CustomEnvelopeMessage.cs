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

            var prefix = Version.Envelope.NamespacePrefix(NamespaceManager);
            writer.WriteStartElement(prefix, "Envelope", Version.Envelope.Namespace());
            writer.WriteXmlnsAttribute(prefix, Version.Envelope.Namespace());
            writer.WriteAttributeString(prefix, "encodingStyle", null, "http://schemas.xmlsoap.org/soap/encoding/");

            var xsdPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsd", Namespaces.XMLNS_XSD);
            writer.WriteXmlnsAttribute(xsdPrefix, Namespaces.XMLNS_XSD);

            var xsiPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsi", Namespaces.XMLNS_XSI);
            writer.WriteXmlnsAttribute(xsiPrefix, Namespaces.XMLNS_XSI);

            if (AdditionalEnvelopeXmlnsAttributes != null)
            {
                foreach (var rec in AdditionalEnvelopeXmlnsAttributes)
                {
                    writer.WriteXmlnsAttribute(rec.Key, rec.Value);
                }
            }

        }
    }
}
