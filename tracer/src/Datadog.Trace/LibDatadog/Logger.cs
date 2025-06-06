// <copyright file="Logger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog;

internal sealed class Logger
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Logger>();
    private static readonly Lazy<Logger> _instance = new(() => new Logger());

    private readonly TracerSettings _tracerSettings;

    private Logger()
    {
        _tracerSettings = TracerSettings.FromDefaultSourcesInternal();
    }

    public static Logger Instance => _instance.Value;

    public void Enable(DatadogLoggingConfiguration config, DomainMetadata domainMetadata)
    {
        if (!_tracerSettings.DataPipelineEnabled)
        {
            return;
        }

        if (config.File is not { } fileConfig)
        {
            return;
        }

        var filePath = Path.Combine(
            fileConfig.LogDirectory,
            $"dotnet-libdatadog-{domainMetadata.ProcessName}-{domainMetadata.ProcessId.ToString(CultureInfo.InvariantCulture)}.log");

        using var path = new CharSlice(filePath);
        var cfg = new FileConfig { Path = path };

        try
        {
            using var error = NativeInterop.Logger.ConfigureFile(cfg);
            error.ThrowIfError();
        }
        catch (Exception ex) when (ex is not LibDatadogException)
        {
            Log.Error(ex, "Failed to configure libdatadog logger");
        }
    }

    public void Disable()
    {
        if (!_tracerSettings.DataPipelineEnabled)
        {
            return;
        }

        try
        {
            using var error = NativeInterop.Logger.DisableFile();
            error.ThrowIfError();
        }
        catch (Exception ex) when (ex is not LibDatadogException)
        {
            Log.Error(ex, "Failed to disable libdatadog logger");
        }
    }

    public void SetLogLevel(Vendors.Serilog.Events.LogEventLevel logLevel)
    {
        if (!_tracerSettings.DataPipelineEnabled)
        {
            return;
        }

        var level = logLevel.ToLogEventLevel();
        try
        {
            using var error = NativeInterop.Logger.SetLogLevel(level);
            error.ThrowIfError();
        }
        catch (Exception ex) when (ex is not LibDatadogException)
        {
            Log.Error(ex, "Failed to set libdatadog log level");
        }
    }
}
