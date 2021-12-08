// <copyright file="LogEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting
{
    internal readonly struct LogEntry
    {
        public LogEntry(
            LogEventInfoProxyBase logEventInfo,
            IDictionary<string, object?>? properties,
            IDictionary<object, object>? fallbackProperties)
        {
            LogEventInfo = logEventInfo;
            Properties = properties;
            FallbackProperties = fallbackProperties;
        }

        public LogEventInfoProxyBase LogEventInfo { get; }

        public IDictionary<string, object?>? Properties { get; }

        public IDictionary<object, object>? FallbackProperties { get; }
    }
}
