using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using MsgPack;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class OriginTagSendTraces
    {
        private readonly Tracer _tracer;
        private readonly TestApi _testApi;

        public OriginTagSendTraces()
        {
            var settings = new TracerSettings();
            _testApi = new TestApi();
            var agentWriter = new AgentWriter(_testApi, statsd: null);
            _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
        }

        [Fact]
        public void NormalSpan()
        {
            var scope = _tracer.StartActive("Operation");
            scope.Dispose();

            var objects = _testApi.Wait();
            var spanDictio = objects[0].FirstDictionary();
            var metaDictio = spanDictio["meta"].AsDictionary();
            Assert.False(metaDictio.ContainsKey(Tags.Origin));
        }

        [Fact]
        public void NormalOriginSpan()
        {
            const string originValue = "ciapp-test";

            using (var scope = _tracer.StartActive("Operation"))
            {
                scope.Span.SetTag(Tags.Origin, originValue);
            }

            var objects = _testApi.Wait();
            var spanDictio = objects[0].FirstDictionary();
            var metaDictio = spanDictio["meta"].AsDictionary();
            Assert.True(metaDictio.ContainsKey(Tags.Origin));
            Assert.Equal(originValue, metaDictio[Tags.Origin]);
        }

        [Fact]
        public void OriginInMultipleSpans()
        {
            const string originValue = "ciapp-test";

            using (var scope = _tracer.StartActive("Operation"))
            {
                scope.Span.SetTag(Tags.Origin, originValue);
                using (var cs1 = _tracer.StartActive("Operation2"))
                {
                    using var cs01 = _tracer.StartActive("Operation2_01");
                }

                using (var cs2 = _tracer.StartActive("Operation2"))
                {
                    using var cs02 = _tracer.StartActive("Operation2_01");
                }
            }

            var objects = _testApi.Wait();
            var objectsList = objects[0].AsList();
            foreach (MessagePackObject objValue in objectsList)
            {
                var spanDictio = objValue.FirstDictionary();
                var metaDictio = spanDictio["meta"].AsDictionary();
                Assert.True(metaDictio.ContainsKey(Tags.Origin));
                Assert.Equal(originValue, metaDictio[Tags.Origin]);
            }
        }

        [Fact]
        public void MultipleOriginsSpans()
        {
            const string originValue = "ciapp-test_";
            var origins = new string[] { originValue + "01", originValue + "02", originValue + "03" };

            using (var scope = _tracer.StartActive("Operation"))
            {
                scope.Span.SetTag(Tags.Origin, originValue + "01");

                using (var cs1 = _tracer.StartActive("Operation2"))
                {
                    cs1.Span.SetTag(Tags.Origin, originValue + "02");

                    using var cs01 = _tracer.StartActive("Operation2_01");
                }

                using (var cs2 = _tracer.StartActive("Operation2"))
                {
                    cs2.Span.SetTag(Tags.Origin, originValue + "03");

                    using var cs02 = _tracer.StartActive("Operation2_01");
                }
            }

            var objects = _testApi.Wait();
            var objectsList = objects[0].AsList();
            foreach (MessagePackObject objValue in objectsList)
            {
                var spanDictio = objValue.FirstDictionary();
                var metaDictio = spanDictio["meta"].AsDictionary();
                Assert.True(metaDictio.ContainsKey(Tags.Origin));
                var value = metaDictio[Tags.Origin];
                Assert.True(Array.IndexOf(origins, value.ToString()) != -1);
            }
        }
    }
}
