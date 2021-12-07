// <copyright file="NormalizerTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.TraceProcessors
{
    internal class NormalizerTraceProcessor : ITraceProcessor
    {
        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/normalize.go

        // DefaultSpanName is the default name we assign a span if it's missing and we have no reasonable fallback
        private const string DefaultSpanName = "unnamed_operation";
        // DefaultServiceName is the default name we assign a service if it's missing and we have no reasonable fallback
        private const string DefaultServiceName = "unnamed-service";
        // MaxNameLen the maximum length a name can have
        private const int MaxNameLen = 100;
        // MaxServiceLen the maximum length a service can have
        private const int MaxServiceLen = 100;
        // MaxTypeLen the maximum length a span type can have
        private const int MaxTypeLen = 100;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NormalizerTraceProcessor>();
        private static readonly DateTime Year2000Time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            /*
                +----------+--------------------------------------------------------------------------------------------------------+
                | Property |                                                 Action                                                 |
                +----------+--------------------------------------------------------------------------------------------------------+
                | Service  | If empty, it gets set to a default.                                                                    |
                | Service  | If too long, truncated at 100 characters.                                                              |
                | Name     | If empty, set it to “unnamed_operation”.                                                               |
                | Name     | If too long, truncated to 100 characters.                                                              |
                | Resource | If empty, set to the same value as Name.                                                               |
                | Duration | If smaller than 0, it is set to 0.                                                                     |
                | Duration | If larger than math.MaxInt64 - Start, it is set to 0.                                                  |
                | Start    | If smaller than Y2K, set to now - Duration or 0 if the result is negative.                             |
                | Type     | If too long, truncated to 100.                                                                         |
                | Meta     | “http.status_code” key is deleted if it’s an invalid numeric value smaller than 100 or bigger than 600 |
                +----------+--------------------------------------------------------------------------------------------------------+
             */

            foreach (var span in trace)
            {
                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L44-L56
                if (string.IsNullOrEmpty(span.ServiceName))
                {
                    Log.Debug("Fixing malformed trace. Service  is empty (reason:service_empty), setting span.service={serviceName}: {span}", span.ServiceName, span);
                    span.ServiceName = DefaultServiceName;
                }
                else
                {
                    // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/normalize.go#L54-L68
                    var serviceName = span.ServiceName;
                    if (TraceUtil.TruncateUTF8(ref serviceName, MaxServiceLen))
                    {
                        span.ServiceName = serviceName;
                        Log.Debug("Fixing malformed trace. Service is too long (reason:service_truncate), truncating span.service to length={maxServiceLen}: {span}", MaxServiceLen, span);
                    }

                    span.ServiceName = TraceUtil.NormalizeTag(span.ServiceName);
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L69-L80
                if (string.IsNullOrEmpty(span.OperationName))
                {
                    Log.Debug("Fixing malformed trace. Name is empty (reason:span_name_empty), setting span.name={name}: {span}", span.OperationName, span);
                    span.OperationName = DefaultSpanName;
                }
                else
                {
                    var operationName = span.OperationName;
                    if (TraceUtil.TruncateUTF8(ref operationName, MaxNameLen))
                    {
                        span.OperationName = operationName;
                        Log.Debug("Fixing malformed trace. Name is too long (reason:span_name_truncate), truncating span.name to length={maxServiceLen}: {span}", MaxNameLen, span);
                    }
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L82-L86
                if (string.IsNullOrEmpty(span.ResourceName))
                {
                    Log.Debug("Fixing malformed trace. Resource is empty (reason:resource_empty), setting span.resource={name}: {span}", span.OperationName, span);
                    span.ResourceName = span.OperationName;
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L101-L105
                if (span.Duration < TimeSpan.Zero)
                {
                    Log.Debug("Fixing malformed trace. Duration is invalid (reason:invalid_duration), setting span.duration=0: {span}", span.OperationName, span);
                    span.SetDuration(TimeSpan.Zero);
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L106-L110
                if (span.Duration.ToNanoseconds() > long.MaxValue - span.StartTime.ToUnixTimeNanoseconds())
                {
                    Log.Debug("Fixing malformed trace. Duration is too large and causes overflow (reason:invalid_duration), setting span.duration=0: {span}", span.OperationName, span);
                    span.SetDuration(TimeSpan.Zero);
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L111-L119
                if (span.StartTime < Year2000Time)
                {
                    Log.Debug("Fixing malformed trace. Start date is invalid (reason:invalid_start_date), setting span.start=time.now(): {span}", span);
                    var now = span.Context.TraceContext.UtcNow;
                    var start = now - span.Duration;
                    if (start.ToUnixTimeNanoseconds() < 0)
                    {
                        start = now;
                    }

                    span.SetStartTime(start);
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L121-L125
                var type = span.Type;
                if (TraceUtil.TruncateUTF8(ref type, MaxTypeLen))
                {
                    span.Type = type;
                    Log.Debug("Fixing malformed trace. Type is too long (reason:type_truncate), truncating span.type to length={maxServiceLen}: {span}", MaxTypeLen, span);
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L126-L128
                if (span.Tags is CommonTags commonTags)
                {
                    commonTags.Environment = TraceUtil.NormalizeTag(commonTags.Environment);
                }
                else
                {
                    string env = span.GetTag("env");
                    if (!string.IsNullOrEmpty(env))
                    {
                        span.SetTag("env", TraceUtil.NormalizeTag(env));
                    }
                }

                // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L129-L135
                if (span.Tags is IHasStatusCode statusCodeTags)
                {
                    if (!TraceUtil.IsValidStatusCode(statusCodeTags.HttpStatusCode))
                    {
                        statusCodeTags.HttpStatusCode = string.Empty;
                    }
                }
                else
                {
                    string httpStatusCode = span.GetTag(Tags.HttpStatusCode);
                    if (!string.IsNullOrEmpty(httpStatusCode) && !TraceUtil.IsValidStatusCode(httpStatusCode))
                    {
                        span.SetTag(Tags.HttpStatusCode, null);
                    }
                }
            }

            return trace;
        }
    }
}
