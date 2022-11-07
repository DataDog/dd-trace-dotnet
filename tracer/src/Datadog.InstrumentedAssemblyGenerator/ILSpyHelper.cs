using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using IMethod = ICSharpCode.Decompiler.TypeSystem.IMethod;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal static class ILSpyHelper
    {
        internal static string GetMethodAndParametersName(string name, IReadOnlyList<IParameter> parameters)
        {
            return name + $"({string.Join(",", parameters.Select(p => SanitizeTypeName(p.Type.FullName)))})";
        }

        internal static string SanitizeTypeName(string typeName)
        {
            return typeName.Replace("<", "[[").Replace(">", "]]").Replace("/", "+");
        }

        internal static ITypeDefinition FindType(CSharpDecompiler decompiler, string typeName)
        {
            return decompiler.TypeSystem.MainModule.Compilation.FindType(new FullTypeName(SanitizeTypeName(typeName))).GetDefinition();
        }

        internal static IMethod FindMethod(ITypeDefinition type, string methodFullName)
        {
            return type.Methods.SingleOrDefault(m => GetMethodAndParametersName(m.Name, m.Parameters) == methodFullName) ??
                                     type.GetConstructors().SingleOrDefault(m => GetMethodAndParametersName(m.Name.Substring(1), m.Parameters) == methodFullName);

        }
    }
}
