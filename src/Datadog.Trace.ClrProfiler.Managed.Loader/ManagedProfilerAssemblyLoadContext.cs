#if NETCOREAPP
using System.Reflection;
using System.Runtime.Loader;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    internal class ManagedProfilerAssemblyLoadContext : AssemblyLoadContext
    {
        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
#endif
