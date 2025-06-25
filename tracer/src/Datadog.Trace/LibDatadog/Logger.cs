// <copyright file="Logger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog;

internal sealed class Logger
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Logger>();

    private bool _loggingEnabled = false;

    public static Logger Instance { get; } = new();

    public void Enable(FileLoggingConfiguration fileConfig, DomainMetadata domainMetadata)
    {
        // Rebuild the logger even if it has already been enabled, in case any configuration has changed

        var filePath = Path.Combine(
            fileConfig.LogDirectory,
            $"dotnet-tracer-libdatadog-{domainMetadata.ProcessName}-{domainMetadata.ProcessId.ToString(CultureInfo.InvariantCulture)}.log");

        using var path = new CharSlice(filePath);
        var cfg = new FileConfig
        {
            Path = path,
            MaxFiles = 31, // Matches existing Serilog configuration
            MaxSizeBytes = (ulong)fileConfig.MaxLogFileSizeBytes,
        };

        try
        {
            Log.Debug("Enabling libdatadog logger");
            using var error = NativeInterop.Logger.ConfigureFile(cfg);
            error.ThrowIfError();
            _loggingEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to configure libdatadog logger");
        }
    }

    public void Disable()
    {
        if (!_loggingEnabled)
        {
            Log.Debug("Libdatadog logging is not enabled, skipping disable operation.");
            return;
        }

        try
        {
            Log.Debug("Disabling libdatadog logger");
            using var error = NativeInterop.Logger.DisableFile();
            error.ThrowIfError();
            _loggingEnabled = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to disable libdatadog logger");
        }
    }

    public void SetLogLevel(bool debugEnabled)
    {
        var logLevel = debugEnabled ? Vendors.Serilog.Events.LogEventLevel.Debug : DatadogLogging.DefaultLogLevel;
        SetLogLevel(logLevel);
    }

    private void SetLogLevel(Vendors.Serilog.Events.LogEventLevel logLevel)
    {
        if (!_loggingEnabled)
        {
            // This could happen if, for example, datapipeline is enabled, and we trigger a tracer flare to change the log level.
            Log.Information("Attempted to set libdatadog log level to {LogLevel} without enabling logging first", logLevel);
            return;
        }

        var level = logLevel.ToLogEventLevel();
        try
        {
            using var error = NativeInterop.Logger.SetLogLevel(level);
            error.ThrowIfError();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set libdatadog log level");
        }
    }
}
