namespace CbsCatVerifier
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            string cabFile = args[0];

            return CatalogBasedVerification.CatalogBasedVerifier(cabFile);
        }
    }
}
