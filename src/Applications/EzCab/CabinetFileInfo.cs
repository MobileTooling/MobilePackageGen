namespace EzCab
{
    public class CabinetFileInfo
    {
        public string FileName
        {
            get; set;
        }

        public FileAttributes Attributes
        {
            get; set;
        }

        public DateTime DateTime
        {
            get; set;
        }

        public Stream FileStream
        {
            get; set;
        }

        public (Stream, FileAttributes, DateTime) GetFileTuple()
        {
            return (FileStream, Attributes, DateTime);
        }
    }
}
