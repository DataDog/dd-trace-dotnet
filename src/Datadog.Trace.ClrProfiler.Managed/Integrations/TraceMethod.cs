using System;
using Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// A custom attribute to control tracing.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class TraceMethod : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceMethod"/> class.
        /// </summary>
        /// <param name="assembly">The assembly the source method can be found in.</param>
        /// <param name="typeName">The name of the type the method can be found in.</param>
        /// <param name="method">The method to replace.</param>
        public TraceMethod(string assembly, string typeName, string method)
        {
            Assembly = assembly;
            Type = typeName;
            Method = method;
        }

        /// <summary>
        /// Gets the assembly the source method can be found in.
        /// </summary>
        public string Assembly { get; private set; }

        /// <summary>
        /// Gets the type the method can be found in.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Gets the method name.
        /// </summary>
        public string Method { get; private set; }
    }
}
