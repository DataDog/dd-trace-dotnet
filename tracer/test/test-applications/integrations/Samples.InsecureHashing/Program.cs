using System.Collections;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.InsecureHash
{
    internal static class Program
    {
        private static void Main()
        {
            //Vulnerable section

            testHashAlgorithm(new HMACMD5(new byte[] { 4, 4 }));
            testHashAlgorithm(new MD5CryptoServiceProvider());
            testHashAlgorithm(MD5.Create());
            testHashAlgorithm(new HMACSHA1(new byte[] { 4, 4 }));
            testHashAlgorithm(SHA1.Create());
            testHashAlgorithm(new SHA1CryptoServiceProvider());
            testHashAlgorithm(HMAC.Create("HMACMD5"));

            // not vulnerable section

            testHashAlgorithm(new testHash());
            testHashAlgorithm(HMAC.Create("HMACSHA512"));
            testHashAlgorithm(SHA512.Create());
            testHashAlgorithm(new HMACSHA512());
            testHashAlgorithm(SHA384.Create());
            testHashAlgorithm(new HMACSHA384());
            testHashAlgorithm(SHA256.Create());
            testHashAlgorithm(new HMACSHA256());

#if NET461
            testHashAlgorithm(new HMACRIPEMD160(new byte[] { 4, 4 }));
            testHashAlgorithm(RIPEMD160Managed.Create());
            testHashAlgorithm(new MACTripleDES());
            testHashAlgorithm(MACTripleDES.Create());
#endif
        }

        private static void testHashAlgorithm(HashAlgorithm algorithm)
        {
            var byteArg = new byte[] { 3, 5, 6 };
            var stream = new MemoryStream(byteArg);

            algorithm.ComputeHash(byteArg, 0, 3);
            algorithm.ComputeHash(byteArg);
            algorithm.ComputeHash(stream);
#if NET50 || NET60
            _ = algorithm.ComputeHashAsync(stream, CancellationToken.None).Result;
#endif
        }
    }
}
