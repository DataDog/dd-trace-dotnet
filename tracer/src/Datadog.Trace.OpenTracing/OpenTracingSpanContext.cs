// <copyright file="OpenTracingSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpanContext : global::OpenTracing.ISpanContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OpenTracingSpanContext>();

        public OpenTracingSpanContext(ISpanContext context)
        {
            Context = context;
        }

        public string TraceId => Context.TraceId.ToString(CultureInfo.InvariantCulture);

        public string SpanId => Context.SpanId.ToString(CultureInfo.InvariantCulture);

        internal ISpanContext Context { get; }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
