using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ContentDirectory
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "X_GetFeatureListResponse")]
    [XmlRoot(ElementName = "X_GetFeatureListResponse")]
    public class X_GetFeatureList
    {
        [XmlArray(ElementName = "FeatureList")]
        [XmlArrayItem(ElementName = "Feature")]
        public List<string> FeatureList { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
