using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.AssemblyLoadContext
{
    /// <summary>
    /// Assembly Builder Helper
    /// </summary>
    public static class AssemblyBuilderHelper
    {
        /// <summary>
        /// Define Dynamic Assembly
        /// </summary>
        /// <param name="name">Assembly name</param>
        /// <returns>AssemblyBuilder instance</returns>
        public static AssemblyBuilder DefineDynamicAssembly(string name)
        {
            return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        }
    }
}
