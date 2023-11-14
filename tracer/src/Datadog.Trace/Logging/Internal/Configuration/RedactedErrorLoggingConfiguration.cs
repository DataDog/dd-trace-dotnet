// <copyright file="RedactedErrorLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Telemetry.Collectors;

namespace Datadog.Trace.Logging.Internal.Configuration;

internal class RedactedErrorLoggingConfiguration
{
    public RedactedErrorLoggingConfiguration(RedactedErrorLogCollector collector)
    {
        Collector = collector;
    }

    public RedactedErrorLogCollector Collector { get; }
}
