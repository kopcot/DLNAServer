﻿using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ContentDirectory
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetSortCapabilitiesResponse")]
    [XmlRoot(ElementName = "GetSortCapabilitiesResponse")]
    public class GetSortCapabilities
    {
        [XmlElement(ElementName = "SearchCaps")]
        public string SortCaps { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
