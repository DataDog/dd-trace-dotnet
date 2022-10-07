using System.Security.Cryptography;

namespace Samples.WeakHashing
{
    internal class CustomMD5 : MD5
    {
        public override void Initialize()
        {
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
        }

        protected override byte[] HashFinal()
        {
            return new byte[] { 2, 3, 4 };
        }
    }
}
