// <copyright file="ThirdPartyModules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Logging;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty
{
    internal class ThirdPartyModules
    {
        private static HashSet<string>? _thirdPartyModuleNames;

        internal static bool PopulateFromConfig()
        {
            _thirdPartyModuleNames = ThirdPartyConfigurationReader.GetModules();
            return _thirdPartyModuleNames.Count > 0;
        }

        internal static bool Contains(string moduleName)
        {
            return _thirdPartyModuleNames?.Contains(moduleName) == true;
        }
    }
}
