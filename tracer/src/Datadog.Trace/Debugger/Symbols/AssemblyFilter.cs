// <copyright file="AssemblyFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class AssemblyFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AssemblyFilter));

        internal static bool ShouldSkipAssembly(Assembly assembly, HashSet<string>? includeList = null)
        {
            var assemblyName = assembly.GetName().Name;
            return string.IsNullOrWhiteSpace(assemblyName) ||
                   assembly.IsDynamic ||
                   assembly.ManifestModule.IsResource() ||
                   string.IsNullOrWhiteSpace(assembly.Location) ||
                   IsDatadogAssembly(assemblyName) ||
                   IsThirdPartyCode(assemblyName) ||
                   (includeList != null && !IsInIncludeList(assemblyName, includeList));
        }

        private static bool IsThirdPartyCode(string assemblyName)
        {
            return ThirdPartyModules.Contains(GetAssemblyNameWithoutExtension(assemblyName));
        }

        internal static bool IsDatadogAssembly(string? assemblyName)
        {
            return assemblyName?.StartsWith("datadog.", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsInIncludeList(string assemblyName, HashSet<string>? includeList)
        {
            return includeList?.Contains(assemblyName) == true;
        }

        private static string? GetAssemblyNameWithoutExtension(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return assemblyName;
            }

            try
            {
                var lastPeriod = assemblyName.LastIndexOf('.');
                if (lastPeriod == -1)
                {
                    return assemblyName;
                }

                if (lastPeriod == assemblyName.Length - 1)
                {
                    return assemblyName.Substring(0, assemblyName.Length - 1);
                }

                var ext = assemblyName.Remove(0, lastPeriod + 1).ToLower();

                if (ext is "dll" or "exe" or "so")
                {
                    return assemblyName.Substring(0, assemblyName.Length - ext.Length);
                }

                return assemblyName;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get the name of {AssemblyName} without extension", assemblyName);
                return null;
            }
        }
    }
}
