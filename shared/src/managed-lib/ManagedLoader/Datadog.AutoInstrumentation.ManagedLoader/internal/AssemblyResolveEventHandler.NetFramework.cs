#if NETFRAMEWORK

using System;
using System.Reflection;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    /// <summary>
    /// See main description in <c>AssemblyLoader.cs</c>
    /// </summary>
    internal partial class AssemblyResolveEventHandler
    {
        public Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = ParseAssemblyName(args?.Name);
            return OnAssemblyResolveCore(assemblyName);
        }
    }
}

#endif
