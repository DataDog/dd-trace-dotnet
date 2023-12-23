// <copyright file="AssemblyFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class AssemblyFilter
    {
        internal static bool ShouldSkipAssembly(Assembly assembly, HashSet<string>? includeList = null)
        {
            var assemblyName = assembly.GetName().Name;
            return assembly.IsDynamic ||
                   assembly.ManifestModule.IsResource() ||
                   string.IsNullOrWhiteSpace(assembly.Location) ||
                   string.IsNullOrWhiteSpace(assemblyName) ||
                   IsThirdPartyCode(assemblyName) ||
                   IsDatadogAssembly(assemblyName) ||
                   (includeList != null && !IsInIncludeList(assemblyName, includeList));
        }

        private static bool IsThirdPartyCode(string assemblyName)
        {
            // This implementation is just a stub - we will need to replace it
            // with a proper implementation in the future.
            string[] thirdPartyStartsWith = { "Microsoft", "System", "netstandard" };
            return thirdPartyStartsWith.Any(t => assemblyName?.StartsWith(t, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static bool IsDatadogAssembly(string assemblyName)
        {
            return assemblyName?.StartsWith("datadog.", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsInIncludeList(string assemblyName, HashSet<string>? includeList)
        {
            return includeList?.Contains(assemblyName) == true;
        }
    }
}
