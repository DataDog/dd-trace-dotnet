using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace AssemblyLoadContextRedirect;

public class CustomAssemblyLoadContext : AssemblyLoadContext
{
    public CustomAssemblyLoadContext()
#if NETCOREAPP3_0_OR_GREATER
            :base("CustomAssemblyLoadContext")
#endif
    {
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName.Name + ".dll");

        if (File.Exists(path))
        {
            Console.WriteLine($"Loading {assemblyName.FullName} from disk");
            return LoadFromAssemblyPath(path);
        }

#if NETCOREAPP3_0_OR_GREATER
        return base.Load(assemblyName);
#else
        return null;
#endif
    }
}
