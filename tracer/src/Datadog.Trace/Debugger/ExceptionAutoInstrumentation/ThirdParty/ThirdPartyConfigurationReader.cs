// <copyright file="ThirdPartyConfigurationReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty
{
    internal class ThirdPartyConfigurationReader
    {
        private const string ThirdPartyResourceName = "Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ConfigFiles.third-party-module-names.json";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ThirdPartyConfigurationReader>();

        internal static ImmutableHashSet<string> GetModules()
        {
            try
            {
                return SafeGetModules();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read third party libs from the embedded resource '{ThirdPartyResourceName}'.", ThirdPartyResourceName);
                return ImmutableHashSet<string>.Empty;
            }
        }

        private static ImmutableHashSet<string> SafeGetModules()
        {
            if (!TryGetThirdPartyManifestStream(out var stream))
            {
                return ImmutableHashSet<string>.Empty;
            }

            using var sr = new StreamReader(stream!);
            using var jsonReader = new JsonTextReader(sr);

            var modules = new HashSet<string>();
            while (jsonReader.Read())
            {
                if (jsonReader is { TokenType: JsonToken.String, Value: string moduleName })
                {
                    modules.Add(moduleName);
                }
            }

            return modules.ToImmutableHashSet();
        }

        private static bool TryGetThirdPartyManifestStream(out Stream? resourceStream)
        {
            var assembly = Assembly.GetExecutingAssembly();
            resourceStream = assembly.GetManifestResourceStream(ThirdPartyResourceName);

            if (resourceStream == null)
            {
                Log.Information("Embedded resource '{ThirdPartyResourceName}' not found.", ThirdPartyResourceName);
            }

            return resourceStream != null;
        }
    }
}
