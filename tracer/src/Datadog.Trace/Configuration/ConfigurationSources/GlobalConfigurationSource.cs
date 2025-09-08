// <copyright file="GlobalConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains global datadog configuration.
/// </summary>
internal class GlobalConfigurationSource
{
    private static IConfigurationSource? _dynamicConfigConfigurationSource = null;
    private static ManualInstrumentationConfigurationSource? _manualConfigurationSource = null;
    private static GlobalConfigurationSourceResult _creationResult = CreateDefaultConfigurationSource();
    private static CompositeConfigurationSource _instance = _creationResult.ConfigurationSource;

    /// <summary>
    /// Gets the configuration source instance.
    /// </summary>
    internal static IConfigurationSource Instance => _instance;

    internal static GlobalConfigurationSourceResult CreationResult => _creationResult;

    /// <summary>
    /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
    /// Precedence is as follows:
    /// - fleet hands-off config, if enabled through DD_APPLICATION_MONITORING_CONFIG_FILE_ENABLED
    /// - environment variables
    /// - local hands-off config, if enabled through DD_APPLICATION_MONITORING_CONFIG_FILE_ENABLED
    /// - AppSettings, app/web.config, if .NET Framework and if file exists
    /// - local datadog.json file, if file exists
    /// </summary>
    /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
    internal static GlobalConfigurationSourceResult CreateDefaultConfigurationSource(string? handsOffLocalConfigPath = null, string? handsOffFleetConfigPath = null, bool? isLibdatadogAvailable = null)
    {
        string? message = null;
        Exception? exception = null;
        var resultType = Result.Success;
        var configurationSource = new CompositeConfigurationSource();
        var environmentSource = new EnvironmentConfigurationSource();
        var configBuilder = new ConfigurationBuilder(environmentSource, TelemetryFactory.Config);
        var applicationMonitoringConfigFileEnabled = configBuilder.WithKeys(ConfigurationKeys.ApplicationMonitoringConfigFileEnabled).AsBool(true);
        if (applicationMonitoringConfigFileEnabled)
        {
            var configsResult = ConfiguratorHelper.GetConfiguration(handsOffLocalConfigPath, handsOffFleetConfigPath, isLibdatadogAvailable);
            if (configsResult is { ConfigurationSuccessResult: { } configsValue })
            {
                // fleet managed hands-off config
                configurationSource.Add(new HandsOffConfigurationSource(configsValue.ConfigEntriesFleet, false));
                // env vars
                configurationSource.Add(environmentSource);
                // local managed hands-off config
                configurationSource.Add(new HandsOffConfigurationSource(configsValue.ConfigEntriesLocal, true));
            }
            else
            {
                message = configsResult.ErrorMessage;
                exception = configsResult.Exception;
                resultType = configsResult.Result;
                // env vars only
                configurationSource.Add(environmentSource);
            }
        }
        else
        {
            resultType = Result.ApplicationMonitoringConfigFileDisabled;
            message = $"{nameof(ConfigurationKeys.ApplicationMonitoringConfigFileEnabled)} is disabled, not using hands-off configuration";
            // env vars only
            configurationSource.Add(environmentSource);
        }

#if NETFRAMEWORK
        // on .NET Framework only, also read from app.config/web.config
        configurationSource.Add(new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings, ConfigurationOrigins.AppConfig));
#endif

        // datadog.json
        if (TryLoadJsonConfigurationFile(configurationSource, null, out var jsonConfigurationSource))
        {
            configurationSource.Add(jsonConfigurationSource);
        }

        return new(configurationSource, resultType, message, exception);
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
    /// <param name="isLibdatadogAvailable">whether libdatadog is available</param>
    internal static void Reload(bool isLibdatadogAvailable)
    {
        _creationResult = CreateDefaultConfigurationSource(isLibdatadogAvailable: isLibdatadogAvailable);
    }

    private static string GetCurrentDirectory()
    {
        return AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
    }

    public static IConfigurationSource UpdateDynamicConfigConfigurationSource(IConfigurationSource dynamic)
    {
        var global = _creationResult.ConfigurationSource;
        Interlocked.Exchange(ref _dynamicConfigConfigurationSource, dynamic);
        var manual = _manualConfigurationSource;
        var combined = CreateMutableConfigurationSource(dynamic, manual, global);
        Interlocked.Exchange(ref _instance, combined);
        return combined;
    }

    public static void UpdateManualConfigurationSource(ManualInstrumentationConfigurationSource manual)
    {
        var global = _creationResult.ConfigurationSource;
        Interlocked.Exchange(ref _manualConfigurationSource, manual);
        var dynamic = _dynamicConfigConfigurationSource;
        var combined = CreateMutableConfigurationSource(dynamic, manual, global);
        Interlocked.Exchange(ref _instance, combined);
        return combined;
    }

    // Internal for testing only
    internal static CompositeConfigurationSource CreateMutableConfigurationSource(
        DynamicConfigConfigurationSource? dynamicConfigConfigurationSource,
        ManualInstrumentationConfigurationSource? manualInstrumentationConfigurationSource,
        CompositeConfigurationSource globalConfiguration)
    {
        // create a config source with the following priority
        // - dynamic config (highest prio)
        // - manual code config
        // - remaining config (see CreateDefaultConfigurationSource)
        if (dynamicConfigConfigurationSource is null)
        {
            return manualInstrumentationConfigurationSource is null
                       ? globalConfiguration
                       : new CompositeConfigurationSource([manualInstrumentationConfigurationSource, globalConfiguration]);
        }

        return manualInstrumentationConfigurationSource is null
                   ? new CompositeConfigurationSource([dynamicConfigConfigurationSource, globalConfiguration])
                   : new CompositeConfigurationSource([dynamicConfigConfigurationSource, manualInstrumentationConfigurationSource, globalConfiguration]);
    }
}
