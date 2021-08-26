#if NETCOREAPP
using System.Reflection;
using System.Runtime.Loader;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    internal class SxSAssemblyLoadContext : AssemblyLoadContext
    {
        public static readonly AssemblyLoadContext SingeltonInstance = new SxSAssemblyLoadContext();

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
#endif
