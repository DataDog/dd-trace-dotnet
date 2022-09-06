using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Samples.InsecureHash
{
    internal static class Program
    {
        private static async Task Main()
        {
            var byteArg = new byte[] {3,5,6};
            new HMACMD5().ComputeHash(byteArg, 0, 3);
            new HMACMD5().ComputeHash(byteArg);
            /*
            HMACRIPEMD160.ComputeHash();
            HMACSHA1.ComputeHash();
            MD5.ComputeHash(byteArg, 10, 10);
            MD5CryptoServiceProvider().ComputeHash();
            RIPEMD160().ComputeHash();
            RIPEMD160Managed.ComputeHash();
            SHA1.ComputeHash();
            SHA1CryptoServiceProvider().ComputeHash();*/
        }
    }
}
