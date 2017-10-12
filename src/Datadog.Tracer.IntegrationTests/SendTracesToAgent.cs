using Microsoft.Reactive.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.IntegrationTests
{
    public class SendTracesToAgent
    {
        [Fact]
        public void MinimalSpan()
        {
            var scheduler = new TestScheduler();
            var tracer = TracerFactory.GetTracer(new Uri("http://localhost:8126"), scheduler);
            var services = tracer.AsList<ServiceInfo>();
            var traces = tracer.AsList<List<Span>>();
            tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .Start()
                .Finish();
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            var trace = traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, services.Count);
        }

        [Fact]
        public void CustomServiceName()
        {
            var tracer = new Tracer();
            var traces = tracer.AsList<List<Span>>();
            var services = tracer.AsList<ServiceInfo>();
            tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .WithTag(Tags.ServiceName, "Service1")
                .Start()
                .Finish();

            var trace = traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, services.Count);
        }

        [Fact]
        public void Utf8Everywhere()
        {
            var tracer = new Tracer();
            var traces = tracer.AsList<List<Span>>();
            var services = tracer.AsList<ServiceInfo>();
            tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                .WithTag(Tags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                .WithTag(Tags.ServiceName, "На берегу пустынных волн")
                .WithTag("யாமறிந்த", "ნუთუ კვლა")
                .Start()
                .Finish();

            var trace = traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, services.Count);
        }
    }
}
