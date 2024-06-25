using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal static class StringExtensions
    {
        public static unsafe void CopyTo(this string value, uint count, char* buffer, uint* pCount)
        {
            uint toCopy = Math.Min((uint)value.Length, count - 1);

            var stringPtrSize = toCopy * 2;
            fixed (char* sPointer = value)
            {
                Buffer.MemoryCopy(sPointer, (void*)buffer, stringPtrSize, stringPtrSize);
                buffer[toCopy] = '\0'; // null-terminate the string
            }

            if (pCount != null)
            {
                *pCount = toCopy;
            }
        }
    }
}
