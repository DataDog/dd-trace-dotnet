// <copyright file="ProcessConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Datadog.Trace.Tools.Shared;
using Datadog.Trace.Tools.Shared.Windows;

namespace Datadog.Trace.Tools.dd_dotnet.Checks;

internal static class ProcessConfiguration
{
    private static IReadOnlyDictionary<string, string>? LoadApplicationConfig(string? mainModule)
    {
        if (mainModule == null)
        {
            return null;
        }

        var folder = Path.GetDirectoryName(mainModule);

        if (folder == null)
        {
            return null;
        }

        var configFileName = Path.GetFileName(mainModule) + ".config";
        var configPath = Path.Combine(folder, configFileName);

        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(configPath);

            var appSettings = document.Element("configuration")?.Element("appSettings");

            if (appSettings == null)
            {
                return null;
            }

            var settings = new Dictionary<string, string>();

            foreach (var setting in appSettings.Elements())
            {
                var key = setting.Attribute("key")?.Value;
                var value = setting.Attribute("value")?.Value;

                if (key != null)
                {
                    settings[key] = value ?? string.Empty;
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            Utils.WriteWarning($"An error occured while parsing the configuration file {configPath}: {ex.Message}");
            return null;
        }
    }

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
            var appConfigSource = LoadApplicationConfig(process.MainModule);

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
