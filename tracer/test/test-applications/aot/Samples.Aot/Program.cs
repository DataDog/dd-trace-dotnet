using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Samples.Aot
{
    [DefaultMember("Main")]
    internal class Program
    {
        [CLSCompliant(true)]
        static void Main(string[] args)
        {
            DoWork();
        }

        static void DoWork()
        {
            Console.WriteLine("Hello, " + AotText + " World!");
        }

        [IgnoreDataMember]
        static string AotText => "AOT";

/*
        static int _isAssemblyLoaded = 0;
        private static void InitDD()
        {
            if (Interlocked.CompareExchange(ref _isAssemblyLoaded, 1, 0) != 1)
            {
                GetAssemblyAndSymbolsBytes(out IntPtr intPtr, out int num, out IntPtr intPtr2, out int num2);
                byte[] array = new byte[num];
                Marshal.Copy(intPtr, array, 0, num);
                byte[] array2 = new byte[num2];
                Marshal.Copy(intPtr2, array2, 0, num2);
                Assembly.Load(array, array2).CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup");
            }
        }

        [System.Runtime.InteropServices.DllImport("Datadog.Tracer.Native.dll", CallingConvention = CallingConvention.StdCall)]
        static extern void GetAssemblyAndSymbolsBytes(out IntPtr pAssemblyArray, out int assemblySize, out IntPtr pSymbolsArray, out int symbolsSize);
*/

    }
}
