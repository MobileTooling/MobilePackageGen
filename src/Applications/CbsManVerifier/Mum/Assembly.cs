
using System.Xml.Serialization;

namespace CbsManVerifier.Mum
{
    [XmlRoot(ElementName = "assemblyIdentity", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class AssemblyIdentity
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "version")]
        public string Version
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "processorArchitecture")]
        public string ProcessorArchitecture
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "language")]
        public string Language
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "buildType")]
        public string BuildType
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "publicKeyToken")]
        public string PublicKeyToken
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "versionScope")]
        public string VersionScope
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "type")]
        public string Type
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "infFile", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class InfFile
    {
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "Transform", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
    public class Transform
    {
        [XmlAttribute(AttributeName = "Algorithm")]
        public string Algorithm
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "Transforms", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
    public class Transforms
    {
        [XmlElement(ElementName = "Transform", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public Transform Transform
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "DigestMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
    public class DigestMethod
    {
        [XmlAttribute(AttributeName = "Algorithm")]
        public string Algorithm
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "hash", Namespace = "urn:schemas-microsoft-com:asm.v2")]
    public class Hash
    {
        [XmlElement(ElementName = "Transforms", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public Transforms Transforms
        {
            get; set;
        }
        [XmlElement(ElementName = "DigestMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public DigestMethod DigestMethod
        {
            get; set;
        }
        [XmlElement(ElementName = "DigestValue", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public string DigestValue
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "asmv2", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Asmv2
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "dsig", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Dsig
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "file", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class File
    {
        [XmlElement(ElementName = "infFile", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public InfFile InfFile
        {
            get; set;
        }
        [XmlElement(ElementName = "hash", Namespace = "urn:schemas-microsoft-com:asm.v2")]
        public Hash Hash
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "sourceName")]
        public string SourceName
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "importPath")]
        public string ImportPath
        {
            get; set;
        }
        [XmlElement(ElementName = "securityDescriptor", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public SecurityDescriptor SecurityDescriptor
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "destinationPath")]
        public string DestinationPath
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "securityDescriptor", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class SecurityDescriptor
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "registryValue", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class RegistryValue
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "valueType")]
        public string ValueType
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "value")]
        public string Value
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "registryKey", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class RegistryKey
    {
        [XmlElement(ElementName = "registryValue", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public List<RegistryValue> RegistryValue
        {
            get; set;
        }
        [XmlElement(ElementName = "securityDescriptor", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public SecurityDescriptor SecurityDescriptor
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "keyName")]
        public string KeyName
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "registryKeys", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class RegistryKeys
    {
        [XmlElement(ElementName = "registryKey", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public List<RegistryKey> RegistryKey
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "securityDescriptorDefinition", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class SecurityDescriptorDefinition
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "sddl")]
        public string Sddl
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "operationHint")]
        public string OperationHint
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "securityDescriptorDefinitions", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class SecurityDescriptorDefinitions
    {
        [XmlElement(ElementName = "securityDescriptorDefinition", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public List<SecurityDescriptorDefinition> SecurityDescriptorDefinition
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "accessControl", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class AccessControl
    {
        [XmlElement(ElementName = "securityDescriptorDefinitions", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public SecurityDescriptorDefinitions SecurityDescriptorDefinitions
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "security", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class Security
    {
        [XmlElement(ElementName = "accessControl", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public AccessControl AccessControl
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "trustInfo", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class TrustInfo
    {
        [XmlElement(ElementName = "security", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public Security Security
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "deconstructionTool", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class DeconstructionTool
    {
        [XmlAttribute(AttributeName = "version")]
        public string Version
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "deployment", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class Deployment
    {
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns
        {
            get; set;
        }
    }

    [XmlRoot(ElementName = "assembly", Namespace = "urn:schemas-microsoft-com:asm.v3")]
    public class Assembly
    {
        [XmlElement(ElementName = "assemblyIdentity", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public AssemblyIdentity AssemblyIdentity
        {
            get; set;
        }
        [XmlElement(ElementName = "file", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public List<File> File
        {
            get; set;
        }
        [XmlElement(ElementName = "registryKeys", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public RegistryKeys RegistryKeys
        {
            get; set;
        }
        [XmlElement(ElementName = "trustInfo", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public TrustInfo TrustInfo
        {
            get; set;
        }
        [XmlElement(ElementName = "deconstructionTool", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public DeconstructionTool DeconstructionTool
        {
            get; set;
        }
        [XmlElement(ElementName = "deployment", Namespace = "urn:schemas-microsoft-com:asm.v3")]
        public Deployment Deployment
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "manifestVersion")]
        public string ManifestVersion
        {
            get; set;
        }
        [XmlAttribute(AttributeName = "copyright")]
        public string Copyright
        {
            get; set;
        }
    }
}