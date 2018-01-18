using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Datadog.Trace
{
    /// <summary>
    /// This class contains factory methods to instantiate a <see cref="Tracer"/>
    /// </summary>
    public class TracerFactory
    {
        private static Uri _defaultUri = new Uri("http://localhost:8126");

        /// <summary>
        /// Create a new Tracer with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="serviceInfoList">The service information list.</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>The newly created tracer</returns>
        public static Tracer GetTracer(Uri agentEndpoint = null, List<ServiceInfo> serviceInfoList = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            agentEndpoint = agentEndpoint ?? _defaultUri;
            return GetTracer(agentEndpoint, serviceInfoList, defaultServiceName, null, isDebugEnabled);
        }

        internal static Tracer GetTracer(Uri agentEndpoint, List<ServiceInfo> serviceInfoList = null, string defaultServiceName = null, DelegatingHandler delegatingHandler = null, bool isDebugEnabled = false)
        {
            var api = new Api(agentEndpoint, delegatingHandler);
            var agentWriter = new AgentWriter(api);
            var tracer = new Tracer(agentWriter, serviceInfoList, defaultServiceName, isDebugEnabled);
            return tracer;
        }
    }
}
