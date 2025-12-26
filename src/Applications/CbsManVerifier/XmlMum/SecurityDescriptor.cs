using System.Xml.Serialization;

namespace MobilePackageGen.XmlMum
{
    [XmlRoot(ElementName = "securityDescriptor", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class SecurityDescriptor
    {
        [XmlAttribute(AttributeName = "name", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public string Name
        {
            get; set;
        }
    }
}
