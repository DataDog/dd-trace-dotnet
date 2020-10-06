using System.Runtime.CompilerServices;

// allow access to internal APIs in Datadog.Trace.dll
[assembly: IgnoresAccessChecksTo("Datadog.Trace")]

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Ignore access checks (e.g. <c>private</c>, <c>internal</c>) when
    /// accessing types or members in the specified assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IgnoresAccessChecksToAttribute"/> class.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly that can be accessed without checks.</param>
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        /// <summary>
        /// Gets the name of the assembly that can be accessed without checks.
        /// </summary>
        public string AssemblyName { get; }
    }
}
