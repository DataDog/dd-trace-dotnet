// <copyright file="ProcessConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Tools.Shared;

namespace Datadog.Trace.Tools.dd_dotnet.Checks;

internal static class ProcessConfiguration
{
    internal static IConfigurationSource ExtractConfigurationSource(this ProcessInfo process,  string? baseDirectory, IReadOnlyDictionary<string, string>? appSettings)
    {
        baseDirectory ??= Path.GetDirectoryName(process.MainModule);

        var configurationSource = new CompositeConfigurationSource();

        configurationSource.Add(new DictionaryConfigurationSource(process.EnvironmentVariables));

        if (appSettings != null)
        {
            configurationSource.Add(new DictionaryConfigurationSource(appSettings));
        }
        else if (process.DotnetRuntime.HasFlag(ProcessInfo.Runtime.NetFx))
        {
            IReadOnlyDictionary<string, string>? appConfigSource = null;

            try
            {
                appConfigSource = process.LoadApplicationConfig();
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to load application configuration: " + ex.Message);
            }

            if (appConfigSource != null)
            {
                configurationSource.Add(new DictionaryConfigurationSource(appConfigSource));
            }
        }

        var jsonConfigurationSource = JsonConfigurationSource.TryLoadJsonConfigurationFile(configurationSource, baseDirectory);

        if (jsonConfigurationSource != null)
        {
            configurationSource.Add(jsonConfigurationSource);
        }

        return configurationSource;
    }
}
