// <copyright file="JsonConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text.Json;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class JsonConfigurationSource : IConfigurationSource
{
    private readonly JsonDocument _document;

    public JsonConfigurationSource(string json)
    {
        _document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
    }

    internal static JsonConfigurationSource? TryLoadJsonConfigurationFile(IConfigurationSource configurationSource, string? baseDirectory)
    {
        // if environment variable is not set, look for default file name in the current directory
        var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName)
                                 ?? configurationSource.GetString("DD_DOTNET_TRACER_CONFIG_FILE")
                                 ?? Path.Combine(baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory, "datadog.json");

        if (string.Equals(Path.GetExtension(configurationFileName), ".JSON", StringComparison.OrdinalIgnoreCase)
            && File.Exists(configurationFileName))
        {
            return new JsonConfigurationSource(File.ReadAllText(configurationFileName));
        }

        return null;
    }

    public string? GetString(string key)
    {
        if (_document.RootElement.TryGetProperty(key, out var property))
        {
            return property.GetString();
        }

        return null;
    }
}
