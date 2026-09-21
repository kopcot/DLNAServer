using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using DlnaServer.Upnp.Soap;
using SoapCore;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Pins the SOAP envelope attributes every control response carries. An LG television listed no
    /// content at all until <see cref="CustomEnvelopeMessage"/> put <c>encodingStyle</c> back on the
    /// envelope, so these attributes are load-bearing rather than decorative.
    /// </summary>
    [TestFixture]
    internal sealed class CustomEnvelopeMessageTest
    {
        private const string SoapAction = "urn:schemas-upnp-org:service:ContentDirectory:1#Browse";
        private const string EncodingStyleUri = "http://schemas.xmlsoap.org/soap/encoding/";

        /// <summary>
        /// The attribute whose absence hid the whole library from an LG television. SoapCore's own
        /// <c>CustomMessage</c> does not emit it, which is why this subclass exists.
        /// </summary>
        [Test]
        public void WriteMessage_PutsTheEncodingStyleAttributeOnTheEnvelope()
        {
            // Arrange, Act
            var xml = WriteEnvelope();

            // Assert
            xml.Should().Contain($"encodingStyle=\"{EncodingStyleUri}\"",
                "because a renderer that requires SOAP 1.1 encodingStyle rejects the response without it, "
                + "and an LG television showed an empty library until this attribute was restored");
        }

        /// <summary>
        /// The reference declares both on the envelope, so a renderer resolving a prefixed type name
        /// against the envelope's scope finds it.
        /// </summary>
        [Test]
        public void WriteMessage_DeclaresTheSchemaAndInstanceNamespacesOnTheEnvelope()
        {
            // Arrange, Act
            var xml = WriteEnvelope();

            // Assert
            xml.Should().Contain("http://www.w3.org/2001/XMLSchema",
                "because the reference declares the schema namespace on the envelope and renderers were "
                + "tested against that form");
            xml.Should().Contain("http://www.w3.org/2001/XMLSchema-instance",
                "because the schema-instance namespace is declared alongside it, for the same reason");
        }

        /// <summary>
        /// The added attributes must not cost the envelope its own identity - a SOAP 1.2 envelope
        /// namespace here would break every renderer at once.
        /// </summary>
        [Test]
        public void WriteMessage_KeepsTheSoap11EnvelopeNamespace()
        {
            // Arrange, Act
            var xml = WriteEnvelope();

            // Assert
            xml.Should().Contain("http://schemas.xmlsoap.org/soap/envelope/",
                "because UPnP control is SOAP 1.1 and the envelope namespace is what a renderer matches on");
        }

        private static string WriteEnvelope()
        {
            using var inner = Message.CreateMessage(MessageVersion.Soap11, SoapAction, "probe");
            using var message = new CustomEnvelopeMessage(inner);

            SeedNamespaceLookup(message);

            var builder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
            };

            using (var writer = XmlWriter.Create(builder, settings))
            {
                using (var dictionaryWriter = XmlDictionaryWriter.CreateDictionaryWriter(writer))
                {
                    message.WriteMessage(dictionaryWriter);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// SoapCore populates the namespace lookup from inside its own assembly, so its setter is not
        /// reachable from here - and writing the envelope dereferences it. Reflection is the only seam
        /// the package leaves open short of booting the whole host.
        /// </summary>
        private static void SeedNamespaceLookup(CustomMessage message)
        {
            var setter = typeof(CustomMessage)
                .GetProperty(nameof(CustomMessage.XmlNamespaceLookup))!
                .GetSetMethod(nonPublic: true)!;

            setter.Invoke(message, [new ConcurrentXmlNamespaceLookup()]);
        }
    }
}
