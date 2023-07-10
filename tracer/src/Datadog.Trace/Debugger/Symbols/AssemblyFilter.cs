// <copyright file="AssemblyFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Reflection;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class AssemblyFilter
    {
        internal static bool ShouldSkipAssembly(Assembly assembly)
        {
            return string.IsNullOrWhiteSpace(assembly.Location) ||
                   assembly.IsDynamic ||
                   assembly.ManifestModule.IsResource() ||
                   IsThirdPartyCode(assembly);
        }

        internal static bool IsThirdPartyCode(Assembly loadedAssembly)
        {
            // This implementation is just a stub - we will need to replace it
            // with a proper implementation in the future.
            string[] thirdPartyStartsWith = { "Microsoft", "System" };

            var assemblyName = loadedAssembly.GetName().Name;
            return thirdPartyStartsWith.Any(t => assemblyName.StartsWith(t));
        }
    }
}
