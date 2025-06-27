// <copyright file="UtcTimestampEnricher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging.Internal.Enrichers;

internal class UtcTimestampEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var utcTimestamp = logEvent.Timestamp.ToUniversalTime();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UtcTimestamp", utcTimestamp));
    }
}
