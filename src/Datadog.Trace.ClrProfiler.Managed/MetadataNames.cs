using System;

namespace Datadog.Trace.ClrProfiler
{
    public class MetadataNames
    {
        public string ModuleName { get; }

        public string TypeName { get; }

        public string MethodName { get; }

        public MetadataNames(string moduleName, string typeName, string methodName)
        {
            ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        }
    }
}
