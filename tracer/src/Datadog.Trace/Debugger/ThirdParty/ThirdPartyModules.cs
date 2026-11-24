// <copyright file="ThirdPartyModules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Debugger.ThirdParty
{
    internal static partial class ThirdPartyModules
    {
        // Mirrors skip_assembly_prefixes in dd_profiler_constants.h: the native tracer doesn't track these modules, so probes can't bind in them.
        // Contains ignores include_assemblies on purpose: those are framework code too. NativeModuleFilterTests checks both lists match.
        [TestingAndPrivateOnly]
        internal static readonly string[] NativeSkippedPrefixes =
        [
            "Microsoft.AI",
            "Microsoft.ApplicationInsights",
            "Microsoft.AspNetCore.",
            "Microsoft.Build",
            "Microsoft.CSharp",
            "Microsoft.Extensions",
            "Microsoft.Web.Compilation.Snapshots",
            "Microsoft.Win32.",
            "System.Core",
            "System.Console",
            "System.Collections",
            "System.ComponentModel",
            "System.Diagnostics",
            "System.Drawing",
            "System.EnterpriseServices",
            "System.Globalization",
            "System.IO",
            "System.Linq",
            "System.Net.",
            "System.Private.",
            "System.Reflection",
            "System.Resources",
            "System.Runtime",
            "System.Security.",
            "System.ServiceModel.",
            "System.Text",
            "System.Threading",
            "System.Web.",
            "System.Workflow.",
            "System.Xml",
            "System.Numerics",
        ];

        /// <summary>
        /// Check if a module is a 3rd party module
        /// </summary>
        /// <param name="moduleName">module name to check if it's a 3rd party module</param>
        /// <returns>true if moduleName is null or empty, is in the 3rd party list, or starts with a prefix the native tracer skips</returns>
        internal static bool Contains(string? moduleName)
        {
            if (StringUtil.IsNullOrEmpty(moduleName) || ThirdPartyModuleNames.Contains(moduleName))
            {
                return true;
            }

            foreach (var prefix in NativeSkippedPrefixes)
            {
                if (moduleName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
