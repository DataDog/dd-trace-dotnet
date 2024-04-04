// <copyright file="ThirdPartyModules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty
{
    internal class ThirdPartyModules
    {
        private static readonly Lazy<bool> IsModulesPopulated = new(PopulateFromConfig);
        private static ImmutableHashSet<string> _thirdPartyModuleNames = ImmutableHashSet<string>.Empty;

        internal static bool IsValid => IsModulesPopulated.Value;

        private static bool PopulateFromConfig()
        {
            _thirdPartyModuleNames = ThirdPartyConfigurationReader.GetModules();
            return _thirdPartyModuleNames.Count > 0;
        }

        /// <summary>
        /// Check if a module is a 3rd party module
        /// </summary>
        /// <param name="moduleName">module name to check if it's a 3rd party module</param>
        /// <returns>true if the 3rd party list contains the moduleName or if the list fail to initialized</returns>
        internal static bool Contains(string? moduleName)
        {
            if (string.IsNullOrEmpty(moduleName) || !IsValid)
            {
                return true;
            }

            return _thirdPartyModuleNames.Contains(moduleName!);
        }
    }
}
