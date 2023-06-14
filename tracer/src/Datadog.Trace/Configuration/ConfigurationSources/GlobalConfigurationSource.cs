// <copyright file="GlobalConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains global datadog configuration.
/// </summary>
internal class GlobalConfigurationSource
{
    /// <summary>
    /// Gets the configuration source instance.
    /// </summary>
    internal static IConfigurationSource Instance { get; private set;  } = CreateDefaultConfigurationSource();

    /// <summary>
    /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
    /// AppSettings where available, and a local datadog.json file, if present.
    /// </summary>
    /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
    internal static CompositeConfigurationSource CreateDefaultConfigurationSource()
    {
        // env > AppSettings > datadog.json
        var configurationSource = new CompositeConfigurationSourceInternal();
        configurationSource.AddInternal(new EnvironmentConfigurationSourceInternal());

#if NETFRAMEWORK
        // on .NET Framework only, also read from app.config/web.config
        configurationSource.AddInternal(new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings, ConfigurationOrigins.AppConfig));
#endif

        if (TryLoadJsonConfigurationFile(configurationSource, null, out var jsonConfigurationSource))
        {
            configurationSource.AddInternal(jsonConfigurationSource);
        }

        return configurationSource;
    }

    internal static bool TryLoadJsonConfigurationFile(IConfigurationSource configurationSource, string? baseDirectory, [NotNullWhen(true)] out IConfigurationSource? jsonConfigurationSource)
    {
        try
        {
            var telemetry = TelemetryFactory.Config;

            // if environment variable is not set, look for default file name in the current directory
            var configurationFileName = new ConfigurationBuilder(configurationSource, telemetry)
                                       .WithKeys(ConfigurationKeys.ConfigurationFileName, "DD_DOTNET_TRACER_CONFIG_FILE")
                                       .AsString(
                                            getDefaultValue: () => Path.Combine(baseDirectory ?? GetCurrentDirectory(), "datadog.json"),
                                            validator: null);

            if (string.Equals(Path.GetExtension(configurationFileName), ".JSON", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(configurationFileName))
            {
                jsonConfigurationSource = JsonConfigurationSource.FromFile(configurationFileName, ConfigurationOrigins.DdConfig);
                return true;
            }
        }
        catch (Exception)
        {
            // Unable to load the JSON file from disk
            // The configuration manager should not depend on a logger being bootstrapped yet
            // so do not do anything
        }

        jsonConfigurationSource = default;
        return false;
    }

    /// <summary>
    /// Used to refresh configuration source.
    /// </summary>
    internal static void Reload()
    {
        Instance = CreateDefaultConfigurationSource();
    }

    private static string GetCurrentDirectory()
    {
        return AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
    }
}
