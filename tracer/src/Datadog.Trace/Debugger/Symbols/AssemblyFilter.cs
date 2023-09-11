// <copyright file="AssemblyFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class AssemblyFilter
    {
        internal static bool ShouldSkipAssembly(Assembly assembly)
        {
            return assembly.IsDynamic ||
                   assembly.ManifestModule.IsResource() ||
                   string.IsNullOrWhiteSpace(assembly.Location) ||
                   IsThirdPartyCode(assembly) ||
                   IsDatadogAssembly(assembly);
        }

        internal static bool IsThirdPartyCode(Assembly loadedAssembly)
        {
            // This implementation is just a stub - we will need to replace it
            // with a proper implementation in the future.
            string[] thirdPartyStartsWith = { "Microsoft", "System", "netstandard" };

            var assemblyName = loadedAssembly.GetName().Name;
            return thirdPartyStartsWith.Any(t => assemblyName?.StartsWith(t, StringComparison.OrdinalIgnoreCase) == true);
        }

        internal static bool IsDatadogAssembly(Assembly loadedAssembly)
        {
            var assemblyName = loadedAssembly.GetName().Name;
            return assemblyName?.StartsWith("datadog.", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
