using OpenTracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            return GetTracer(uri);
        }

        internal static Tracer GetTracer(Uri uri, DelegatingHandler delegatingHandler = null)
        {
            var api = new Api(uri, delegatingHandler);
            var tracer = new Tracer();

            // The BufferBlock is converting the synchronous flow of traces into an asynchronous one to make sure we don't block the context that is emiting the traces and drops traces once it's full.
            var tracesBufferBlock = new BufferBlock<List<Span>>(new DataflowBlockOptions { BoundedCapacity = 1000 });
            // bufferBlock.Post() will return false if the trace is dropped
            // TODO:bertrand warn if we're dropping traces
            tracer
                .Subscribe<List<Span>>(x => tracesBufferBlock.Post(x));

            tracesBufferBlock
                .AsObservable()
                // The Buffer operator flushes a batch of data either when it has 1000 traces to flush or if no trace have been flushed for 1 second.
                .Buffer(TimeSpan.FromSeconds(1), 1000)
                // No need to send empty requests to the agent
                .Where(x => x.Any())
                .Subscribe(async x => await api.SendTracesAsync(x));

            // Similar logic for service info
            var servicesBufferBlock = new BufferBlock<ServiceInfo>(new DataflowBlockOptions { BoundedCapacity = 100 });
            // TODO:bertrand warn if we're dropping services
            tracer
                .Subscribe<ServiceInfo>(x => servicesBufferBlock.Post(x));

            servicesBufferBlock
                .AsObservable()
                .Subscribe(async x => await api.SendServiceAsync(x));

            return tracer;
        }
    }
}
