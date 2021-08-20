using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Demo.LoadUnloadPlugin.NetCore31
{
    internal class MockPluginAssemblyLoadContext : AssemblyLoadContext
    {
        public MockPluginAssemblyLoadContext()
            : base(typeof(MockPluginAssemblyLoadContext).FullName, isCollectible: true)
        { }

        protected override Assembly Load(AssemblyName name)
        {
            // Returns null to ensure that dependencies of explicitly loaded assemblies are loaded into the default context.
            // Only assemblies explicitly loaded into this context will exist in this context.
            return null;
        }
    }
}
