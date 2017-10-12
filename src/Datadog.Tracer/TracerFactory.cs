using OpenTracing;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Datadog.Tracer
{
    public static class TracerFactory
    {
        private static Uri _defaultUri = new Uri("http://localhost:8126");

        public static ITracer GetTracer(Uri uri = null)
        {
            uri = uri ?? _defaultUri;
            return GetTracer(uri, Scheduler.Default);
        }

        internal static Tracer GetTracer(Uri uri, IScheduler scheduler)
        {
            var api = new Api(uri);
            var tracer = new Tracer();
            tracer
                .Buffer<List<Span>>(TimeSpan.FromSeconds(1), 1000, scheduler)
                .Subscribe(async x => await api.SendTracesAsync(x));
            return tracer;
        }
    }
}
