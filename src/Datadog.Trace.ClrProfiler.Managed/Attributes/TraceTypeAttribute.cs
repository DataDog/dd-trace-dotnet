using System;

namespace Datadog.Trace.ClrProfiler.Attributes
{
    /// <summary>
    /// An attribute for tracing types
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TraceTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceTypeAttribute"/> class.
        /// </summary>
        /// <param name="assemblyName">The assembly name to trace</param>
        /// <param name="typeName">The type to trace</param>
        /// <param name="callerAssemblyName">An optional caller assembly</param>
        /// <param name="callerTypeName">An optional caller type name</param>
        public TraceTypeAttribute(string assemblyName, string typeName, string callerAssemblyName = null, string callerTypeName = null)
        {
            AssemblyName = assemblyName;
            TypeName = typeName;
            CallerAssemblyName = callerAssemblyName;
            CallerTypeName = callerTypeName;
        }

        /// <summary>
        /// Gets or sets the assembly name
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the type name
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the caller assembly name
        /// </summary>
        public string CallerAssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the caller type name
        /// </summary>
        public string CallerTypeName { get; set; }
    }
}
