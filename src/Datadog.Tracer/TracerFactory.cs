using OpenTracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;

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

            tracer
                .AsyncBuffer<List<Span>>(1000, TimeSpan.FromSeconds(1), 1000)
                // No need to send empty requests to the agent
                .Where(x => x.Any())
                .Subscribe(async x => await api.SendTracesAsync(x));

            tracer
                .AsyncBuffer<ServiceInfo>(100, TimeSpan.FromSeconds(1), 100)
                // No need to send empty requests to the agent
                .Where(x => x.Any())
                .SelectMany(x => x)
                .Subscribe(async x => await api.SendServiceAsync(x));

            return tracer;
        }
    }
}
