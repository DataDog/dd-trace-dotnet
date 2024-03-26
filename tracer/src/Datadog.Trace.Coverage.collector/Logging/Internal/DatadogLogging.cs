// <copyright file="DatadogLogging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging;

/// <summary>
/// Shim to satisfy imported references
/// </summary>
internal class DatadogLogging
{
    private const LogEventLevel DefaultLogLevel = LogEventLevel.Information;
    internal static readonly LoggingLevelSwitch LoggingLevelSwitch = new(DefaultLogLevel);
    private static readonly IDatadogLogger SharedLogger = DatadogSerilogLogger.NullLogger;

    public static IDatadogLogger GetLoggerFor(System.Type classType) => SharedLogger;

    internal static void Reset()
    {
        LoggingLevelSwitch.MinimumLevel = DefaultLogLevel;
    }

    internal static void SetLogLevel(LogEventLevel logLevel)
    {
        LoggingLevelSwitch.MinimumLevel = logLevel;
    }

    internal static void UseDefaultLevel()
    {
        SetLogLevel(DefaultLogLevel);
    }
}
