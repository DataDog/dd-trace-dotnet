using System;
using Xunit;

namespace Datadog.Tracer.IntegrationTests
{
    public class SendTracesToAgent
    {
        [Fact]
        public void MinimalSpan()
        {
            var tracer = new Tracer(new Api(new Uri("http://localhost:8126")));
            tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .Start()
                .Finish();
        }

        [Fact]
        public void CustomServiceName()
        {
            var tracer = new Tracer(new Api(new Uri("http://localhost:8126")));
            tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .WithTag(Tags.ServiceName, "Service1")
                .Start()
                .Finish();
        }

        [Fact]
        public void Utf8Everywhere()
        {
            var tracer = new Tracer(new Api(new Uri("http://localhost:8126")));
            tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                .WithTag(Tags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                .WithTag(Tags.ServiceName, "На берегу пустынных волн")
                .WithTag("யாமறிந்த", "ნუთუ კვლა")
                .Start()
                .Finish();
        }
    }
}
