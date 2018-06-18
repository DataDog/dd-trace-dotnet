using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Holds names obtained through the metadata interface.
    /// </summary>
    public class MetadataNames
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataNames"/> class.
        /// </summary>
        /// <param name="moduleName">The module's name.</param>
        /// <param name="typeName">The type's name.</param>
        /// <param name="methodName">The method's name.</param>
        public MetadataNames(string moduleName, string typeName, string methodName)
        {
            ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        }

        /// <summary>
        /// Gets the module's name.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        /// Gets the type's full name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the method's name.
        /// </summary>
        public string MethodName { get; }
    }
}
