using System.Runtime.InteropServices;

// https://raw.githubusercontent.com/arul-gupta/CatalogAPI/master/CatalogAPI/CatalogAPI/CatalogFunctions.cs

namespace CbsCatVerifier
{
    internal static class CatalogManager
    {
        private static void ParseErrorCallback(uint u1, uint u2, string s)
        {
            Console.WriteLine($"{u1} {u2} {s}");
        }

        internal static void CreateCatalogFile(string fileName)
        {
            CRYPTCATMEMBER? ccm = null;
            try
            {
                CatalogFunctions.PFN_CDF_PARSE_ERROR_CALLBACK pfn = ParseErrorCallback;
                string? s = null; // This null assignment is deliberately done.

                IntPtr cdfPtr = CatalogFunctions.CryptCATCDFOpen(fileName, Marshal.GetFunctionPointerForDelegate(pfn));
                CRYPTCATCDF cdf = (CRYPTCATCDF)Marshal.PtrToStructure(cdfPtr, typeof(CRYPTCATCDF))!; // This call is required else the catlog file creation fails

                do
                {
                    ccm = new CRYPTCATMEMBER
                    {
                        pIndirectData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SIP_INDIRECT_DATA)))
                    };

                    unsafe
                    {
                        IntPtr ptr = Marshal.StringToHGlobalUni(s);
                        s = CatalogFunctions.CryptCATCDFEnumMembersByCDFTagEx(cdfPtr, ptr.ToPointer(), Marshal.GetFunctionPointerForDelegate(pfn), ccm, true, IntPtr.Zero);
                    }
                } while (s != null);

                CatalogFunctions.CryptCATCDFClose(cdfPtr); // This is required to update the .cat with the files details specified in .cdf file.
            }
            catch
            {
                throw;
            }
            finally
            {
                // Free the unmanaged memory.
                if (ccm != null)
                {
                    Marshal.FreeHGlobal(ccm.pIndirectData);
                }
            }
        }

        // Reads the catalog file and returns the list of hashes in the catalog
        internal static List<string> ReadCatalogFile(string fileName)
        {
            List<string> data = [];

            // Get cryptographic service provider (CSP) to be passed to CryptCATOpen
            bool status = CatalogFunctions.CryptAcquireContext(out IntPtr cryptProv, IntPtr.Zero, IntPtr.Zero, CatalogFunctions.PROV_RSA_FULL, 0);

            if (status == false)
            {
                uint err = (uint)Marshal.GetLastWin32Error();

                if (err == CatalogFunctions.NTE_BAD_KEYSET)
                {
                    // No default container was found. Attempt to create it.
                    status = CatalogFunctions.CryptAcquireContext(out cryptProv, IntPtr.Zero, IntPtr.Zero, CatalogFunctions.PROV_RSA_FULL, CatalogFunctions.CRYPT_NEWKEYSET);
                    if (status == false)
                    {
                        err = (uint)Marshal.GetLastWin32Error();
                        throw new Exception("Error in CryptAcquireContext: " + err);
                    }
                }
                else
                {
                    throw new Exception("Error in CryptAcquireContext: " + err);
                }
            }

            // Open the catalog file for reading
            IntPtr hCatalog = CatalogFunctions.CryptCATOpen(fileName, 0, cryptProv, CatalogFunctions.CRYPTCAT_VERSION_1, 0x00000001);

            if (hCatalog == CatalogFunctions.INVALID_HANDLE_VALUE)
            {
                // Unable to open cat file, release the acquired 
                CatalogFunctions.CryptReleaseContext(cryptProv, 0);
                throw new Exception("Unable to read catalog file");
            }

            // Read the catalog members
            IntPtr pMemberPtr = CatalogFunctions.CryptCATEnumerateMember(hCatalog, IntPtr.Zero);
            while (pMemberPtr != IntPtr.Zero)
            {
                CRYPTCATMEMBER cdf = (CRYPTCATMEMBER)Marshal.PtrToStructure(pMemberPtr, typeof(CRYPTCATMEMBER))!;

                // Use the data in cdf
                data.Add(cdf.pwszReferenceTag);

                pMemberPtr = CatalogFunctions.CryptCATEnumerateMember(hCatalog, pMemberPtr);
            }

            // Close the catalog file
            CatalogFunctions.CryptCATClose(hCatalog);

            // Release CSP 
            CatalogFunctions.CryptReleaseContext(cryptProv, 0);

            return data;
        }
    }
}