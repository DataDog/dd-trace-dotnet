using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Logging;
using MessagePack;

namespace Datadog.Trace.ClrProfiler
{
    internal class DatadogAgentMessagePackActivityExporter : IActivityExporter
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<DatadogAgentMessagePackActivityExporter>();
        internal static readonly Func<ActivityCollectorConfiguration, IActivityExporter> Factory = (config) => new DatadogAgentMessagePackActivityExporter();
        private readonly FormatterResolverWrapper _formatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);

        public bool IsSendTracesSupported
        {
            get { return true; }
        }

        public bool IsSendActivitiesSupported
        {
            get { return false; }
        }

        public void SendTraces(IReadOnlyCollection<TraceActivitiesContainer> traces)
        {
            if (traces == null || traces.Count == 0)
            {
                return;
            }

            // @ToDo!
            // Hacky, just trying to get something up and running
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri("http://localhost:8126"), "/v0.4/traces"));

            // Default headers
            request.Headers.Add(AgentHttpHeaderNames.Language, ".NET");
            request.Headers.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);

            // don't add automatic instrumentation to requests from this HttpClient
            request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");

            request.Headers.Add(AgentHttpHeaderNames.TraceCount, traces.Count.ToString());
            request.Method = "POST";

            request.ContentType = "application/msgpack";
            using (var requestStream = request.GetRequestStream())
            {
                MessagePackSerializer.Serialize(requestStream, traces, _formatterResolver);
            }

            try
            {
                var httpWebResponse = (HttpWebResponse)request.GetResponse();
                if (httpWebResponse.StatusCode != HttpStatusCode.OK)
                {
                    // Do something
                }
            }
            catch (Exception ex)
            {
                // TODO: Make this better
                // Well, we tried
                Log.SafeLogError(ex, "New SendTraces failed");
            }

            return;
        }

        public void SendActivities(IReadOnlyCollection<Activity> traces)
        {
            throw new NotSupportedException($"{nameof(DatadogAgentMessagePackActivityExporter)} does not support SendActivities(..).");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed state:
                // . . .
            }

            // Free unmanaged resources
            // Set large fields to null
        }

        // Uncomment/Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DatadogAgentMessagePackExporter()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
