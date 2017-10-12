using OpenTracing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;

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

        internal static Tracer GetTracer(Uri uri, IScheduler scheduler, DelegatingHandler delegatingHandler = null)
        {
            var api = new Api(uri, delegatingHandler);
            var tracer = new Tracer();
            // The BufferBlock is converting the synchronous flow of traces into an asynchronous one to make sure we don't block the context that is emiting the traces and drops traces once it's full.
            var bufferBlock = new BufferBlock<List<Span>>(new DataflowBlockOptions { BoundedCapacity = 1000 });
            // bufferBlock.Post() will return false if the trace is dropped
            // TODO: warn if we're dropping traces
            tracer
                .Subscribe<List<Span>>(x => bufferBlock.Post(x));

            bufferBlock
                .AsObservable()
                // The Buffer operator flushes a batch of data either when it has 1000 traces to flush or if no trace have been flushed for 1 second.
                .Buffer(TimeSpan.FromSeconds(1), 1000, scheduler)
                .Subscribe(async x => await api.SendTracesAsync(x));
            return tracer;
        }
    }
}
