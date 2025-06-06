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

internal class Logger
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Logger>();

    public static void Enable(DatadogLoggingConfiguration config, DomainMetadata domainMetadata)
    {
        if (config.File is { } fileConfig)
        {
            var libdatadogLogPath = Path.Combine(fileConfig.LogDirectory, $"dotnet-libdatadog-{domainMetadata.ProcessName}-{domainMetadata.ProcessId.ToString(CultureInfo.InvariantCulture)}.log");
            using var path = new CharSlice(libdatadogLogPath);
            var cfg = new FileConfig
            {
                Path = path
            };
            try
            {
                NativeInterop.Logger.ConfigureFile(cfg);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure file logger");
            }
        }
    }

    public static void Disable()
    {
        try
        {
            NativeInterop.Logger.DisableFile();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to disable logger");
        }
    }

    public static void SetLogLevel(Vendors.Serilog.Events.LogEventLevel logLevel)
    {
        var level = logLevel.ToLogEventLevel();
        try
        {
            NativeInterop.Logger.SetLogLevel(level);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set log level");
        }
    }
}
