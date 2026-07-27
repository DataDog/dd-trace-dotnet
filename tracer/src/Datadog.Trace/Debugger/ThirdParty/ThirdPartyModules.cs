// <copyright file="ThirdPartyModules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger.ThirdParty
{
    internal static partial class ThirdPartyModules
    {
        /// <summary>
        /// Check if a module is a 3rd party module
        /// </summary>
        /// <param name="moduleName">module name to check if it's a 3rd party module</param>
        /// <returns>true if the 3rd party list contains the moduleName or if the list fail to initialized</returns>
        internal static bool Contains(string? moduleName)
        {
            return StringUtil.IsNullOrEmpty(moduleName)
                || ThirdPartyModuleNames.Contains(moduleName);
        }
    }
}
