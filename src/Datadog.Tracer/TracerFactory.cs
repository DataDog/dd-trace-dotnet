using OpenTracing;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Datadog.Tracer
{
    public static class TracerFactory
    {
        private static Uri _defaultUri = new Uri("http://localhost:8126");

        public static ITracer GetTracer(Uri uri = null, List<ServiceInfo> serviceInfos = null, string defaultServiceName = null)
        {
            uri = uri ?? _defaultUri;
            return GetTracer(uri, serviceInfos, defaultServiceName, null);
        }

        internal static Tracer GetTracer(Uri uri, List<ServiceInfo> serviceInfos = null, string defaultServiceName = null, DelegatingHandler delegatingHandler = null)
        {
            var api = new Api(uri, delegatingHandler);
            var tracer = new Tracer(serviceInfos, defaultServiceName);

            return tracer;
        }
    }
}
