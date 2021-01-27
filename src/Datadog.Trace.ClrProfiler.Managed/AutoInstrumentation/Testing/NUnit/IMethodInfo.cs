using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Interfaces.IMethodInfo
    /// </summary>
    public interface IMethodInfo
    {
        /// <summary>
        /// Gets the MethodInfo for this method.
        /// </summary>
        MethodInfo MethodInfo { get; }
    }
}
