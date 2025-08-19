// <copyright file="LogEventLevelExtension.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.Logging;

internal static class LogEventLevelExtension
{
    public static LogEventLevel ToLogEventLevel(this Vendors.Serilog.Events.LogEventLevel level)
        => level switch
        {
            Vendors.Serilog.Events.LogEventLevel.Verbose => LogEventLevel.Trace,
            Vendors.Serilog.Events.LogEventLevel.Debug => LogEventLevel.Debug,
            Vendors.Serilog.Events.LogEventLevel.Information => LogEventLevel.Info,
            Vendors.Serilog.Events.LogEventLevel.Warning => LogEventLevel.Warn,
            // We don't have a "Fatal" level in libdatadog, so we map everything else to Error
            // We also don't want to risk throwing
            _ => LogEventLevel.Error,
        };
}
