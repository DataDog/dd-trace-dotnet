using OpenTracing;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Datadog.Trace
{
    public static class TracerFactory
    {
        private static Uri _defaultUri = new Uri("http://localhost:8126");

        /// <summary>
        /// Create a  new ITracer object  with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="serviceInfoList">The service information list.</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <returns></returns>
        public static ITracer GetTracer(Uri agentEndpoint = null, List<ServiceInfo> serviceInfoList = null, string defaultServiceName = null)
        {
            agentEndpoint = agentEndpoint ?? _defaultUri;
            return GetTracer(agentEndpoint, serviceInfoList, defaultServiceName, null);
        }

        internal static Tracer GetTracer(Uri agentEndpoint, List<ServiceInfo> serviceInfoList = null, string defaultServiceName = null, DelegatingHandler delegatingHandler = null)
        {
            var api = new Api(agentEndpoint, delegatingHandler);
            var agentWriter = new AgentWriter(api);
            var tracer = new Tracer(agentWriter, serviceInfoList, defaultServiceName);
            return tracer;
        }
    }
}
