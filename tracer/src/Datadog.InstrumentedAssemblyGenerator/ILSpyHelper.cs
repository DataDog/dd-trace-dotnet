using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal static class ILSpyHelper
    {
        internal static string GetMethodAndParametersName(string name, IReadOnlyList<IParameter> parameters)
        {
            return name + $"({string.Join(",", parameters.Select(p => SanitizeParameterTypeName(p.Type.ReflectionName)))})";
        }

        internal static string SanitizeParameterTypeName(string typeName)
        {
            return typeName.Replace("[[", "<").Replace("]]", ">").Replace("[", "").Replace("]", "").Replace("+", "/");
        }
    }
}
