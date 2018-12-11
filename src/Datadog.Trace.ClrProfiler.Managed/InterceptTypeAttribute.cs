using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    ///
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class InterceptTypeAttribute : Attribute
    {
        /// <summary>
        ///
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        ///
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <param name="assembly"></param>
        public InterceptTypeAttribute(string type, string assembly)
        {
            TypeName = type ?? throw new ArgumentNullException(nameof(type));
            AssemblyName = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }
    }
}
