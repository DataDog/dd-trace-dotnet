using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.WeakHashing
{
    internal static class Program
    {
        private static void Main()
        {
            //Vulnerable section
#pragma warning disable SYSLIB0045 // HMAC.Create(string)' is obsolete: 'Cryptographic factory methods accepting an algorithm name are obsolete. Use the parameterless Create factory method on the algorithm type instead
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            testHashAlgorithm(new HMACMD5(new byte[] { 4, 4 }));
            testHashAlgorithm(new MD5CryptoServiceProvider());
            testHashAlgorithm(MD5.Create());
            testHashAlgorithm(new HMACSHA1(new byte[] { 4, 4 }));
            testHashAlgorithm(SHA1.Create());
            testHashAlgorithm(new SHA1CryptoServiceProvider());
            testHashAlgorithm(HMAC.Create("HMACMD5"));
            testHashAlgorithm(new CustomMD5());

#if NETFRAMEWORK
            // This is vulnerable because internally, it is using HMACSHA1
            testHashAlgorithm(MACTripleDES.Create());
#endif
#pragma warning restore SYSLIB0021 // Type or member is obsolete

            // not vulnerable section

            testHashAlgorithm(new testHash());
            testHashAlgorithm(HMAC.Create("HMACSHA512"));
            testHashAlgorithm(SHA512.Create());
            testHashAlgorithm(new HMACSHA512());
            testHashAlgorithm(SHA384.Create());
            testHashAlgorithm(new HMACSHA384());
            testHashAlgorithm(SHA256.Create());
            testHashAlgorithm(new HMACSHA256());

#if NETFRAMEWORK
            testHashAlgorithm(new HMACRIPEMD160(new byte[] { 4, 4 }));
            testHashAlgorithm(RIPEMD160Managed.Create());
            testHashAlgorithm(new MACTripleDES());
#endif
        }

        private static void testHashAlgorithm(HashAlgorithm algorithm)
        {
            var byteArg = new byte[] { 3, 5, 6 };
            var stream = new MemoryStream(byteArg);

            algorithm.ComputeHash(byteArg, 0, 3);
            algorithm.ComputeHash(byteArg);
            algorithm.ComputeHash(stream);
#if NET5_0_OR_GREATER
            _ = algorithm.ComputeHashAsync(stream, CancellationToken.None).Result;
#endif
            System.Threading.Thread.Sleep(100);
        }
    }
}
