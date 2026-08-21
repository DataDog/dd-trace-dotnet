// <copyright file="TagsListTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class TagsListTests : IAsyncLifetime
    {
        private readonly ScopedTracer _tracer;
        private readonly MockApi _testApi;

        public TagsListTests()
        {
            var settings = new TracerSettings();
            _testApi = new MockApi();
            var agentWriter = AgentWriterHelper.CreateWithManualFlush(_testApi);
            _tracer = TracerHelper.Create(settings, agentWriter);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync() => await _tracer.DisposeAsync();

        [Fact]
        [Flaky("This concurrency test can time out on saturated CI agents")]
        public async Task SetTagAndSetTags_WhenCalledConcurrently_ShouldKeepSingleEntryPerKey()
        {
            var tags = new TagsList();

            const int workerCount = 4;
            const int iterationsPerWorker = 1_000;
            var timeout = TimeSpan.FromSeconds(20);
            var expectedKeys = new[] { "k1", "k2", "k3", "k4" };

            using var startSignal = new ManualResetEventSlim(false);
            var workers = Enumerable.Range(0, workerCount)
                                    .Select(
                                         workerId => Task.Run(
                                             () =>
                                             {
                                                 startSignal.Wait();

                                                 for (var i = 0; i < iterationsPerWorker; i++)
                                                 {
                                                     tags.SetTags(
                                                         new("k1", workerId.ToString()),
                                                         new("k2", i.ToString()),
                                                         new("k3", "stable"));
                                                     tags.SetTag("k4", workerId.ToString());
                                                 }
                                             }))
                                    .ToArray();

            startSignal.Set();

            var allWorkers = Task.WhenAll(workers);
            var completedTask = await Task.WhenAny(allWorkers, Task.Delay(timeout));
            if (completedTask != allWorkers)
            {
                throw new TimeoutException($"Concurrent tag updates exceeded {timeout}. Worker statuses: {string.Join(", ", workers.Select(w => w.Status))}");
            }

            await allWorkers;

            var snapshot = GetTagsSnapshot(tags);

            snapshot.Select(x => x.Key).Should().BeEquivalentTo(expectedKeys);
            snapshot.Select(x => x.Key).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void SetTags_WithOnlyNullValues_DoesNotInitializeBackingTagsList()
        {
            var tags = new TagsList();

            tags.SetTags(
                new("k1", null),
                new("k2", null),
                new("k3", null));

            GetBackingTagsList(tags).Should().BeNull();
        }

        [Theory]
        [InlineData(typeof(HttpTags))]
        [InlineData(typeof(HttpV1Tags))]
        [InlineData(typeof(WebTags))]
        [InlineData(typeof(AspNetCoreTags))]
        [InlineData(typeof(AwsSqsTags))]
        [InlineData(typeof(InferredProxyTags))]
        public void SetTag_WithNullValue_RemovesIntBackedTag(Type tagsType)
        {
            var tags = (TagsList)Activator.CreateInstance(tagsType);

            tags.SetTag(Tags.HttpStatusCode, "200");

            ((IHasStatusCode)tags).HttpStatusCode.Should().Be(200);
            tags.GetTag(Tags.HttpStatusCode).Should().Be("200");

            tags.SetTag(Tags.HttpStatusCode, null);

            ((IHasStatusCode)tags).HttpStatusCode.Should().BeNull();
            tags.GetTag(Tags.HttpStatusCode).Should().BeNull();
            GetTagsSnapshot(tags).Select(x => x.Key).Should().NotContain(Tags.HttpStatusCode);
        }

        [Theory]
        [InlineData(typeof(HttpTags))]
        [InlineData(typeof(HttpV1Tags))]
        [InlineData(typeof(WebTags))]
        [InlineData(typeof(AspNetCoreTags))]
        [InlineData(typeof(AwsSqsTags))]
        [InlineData(typeof(InferredProxyTags))]
        public void SetTag_WithUnparseableValue_RemovesIntBackedTag(Type tagsType)
        {
            var tags = (TagsList)Activator.CreateInstance(tagsType);

            tags.SetTag(Tags.HttpStatusCode, "200");
            tags.SetTag(Tags.HttpStatusCode, "not-an-int");

            ((IHasStatusCode)tags).HttpStatusCode.Should().BeNull();
            tags.GetTag(Tags.HttpStatusCode).Should().BeNull();
            GetTagsSnapshot(tags).Select(x => x.Key).Should().NotContain(Tags.HttpStatusCode);
        }

        [Fact]
        public void StronglyTypedStatusCodeAliasesCanBeReadAndWrittenByEitherName()
        {
            var tags = new WebTags();

            tags.SetTag(Tags.HttpStatusCode, "200");
            tags.GetTag(Tags.HttpResponseStatusCode).Should().Be("200");

            tags.SetTag(Tags.HttpResponseStatusCode, "201");
            tags.GetTag(Tags.HttpStatusCode).Should().Be("201");
        }

        [Theory]
        [InlineData(false, Tags.HttpStatusCode)]
        [InlineData(true, Tags.HttpResponseStatusCode)]
        public void StronglyTypedStatusCodeAliasEnumeratesSelectedName(bool openTelemetrySemanticsEnabled, string expectedKey)
        {
            var tags = new WebTags { HttpStatusCode = 202 };

            var snapshot = GetTagsSnapshot(tags, openTelemetrySemanticsEnabled);

            snapshot
               .Where(x => x.Key == Tags.HttpStatusCode || x.Key == Tags.HttpResponseStatusCode)
               .Should()
               .ContainSingle()
               .Which
               .Should()
               .Be(new KeyValuePair<string, string>(expectedKey, "202"));
        }

        [Theory]
        [InlineData(Tags.HttpMethod, Tags.HttpRequestMethod)]
        [InlineData(Tags.HttpUserAgent, Tags.UserAgentOriginal)]
        [InlineData(Tags.HttpClientIp, Tags.ClientAddress)]
        [InlineData(Tags.NetworkClientIp, Tags.NetworkPeerAddress)]
        public void WebTagsAliasesCanBeReadAndWrittenByEitherName(string datadogName, string otelName)
        {
            var tags = new WebTags();

            tags.SetTag(datadogName, "first");
            tags.GetTag(otelName).Should().Be("first");

            tags.SetTag(otelName, "second");
            tags.GetTag(datadogName).Should().Be("second");

            // clearing via either name clears the single backing value
            tags.SetTag(otelName, null);
            tags.GetTag(datadogName).Should().BeNull();
        }

        [Theory]
        [InlineData(false, Tags.HttpMethod, Tags.HttpRequestMethod)]
        [InlineData(true, Tags.HttpRequestMethod, Tags.HttpMethod)]
        public void WebTagsAliasesEnumerateExactlyOneName(bool openTelemetrySemanticsEnabled, string expectedKey, string unexpectedKey)
        {
            var tags = new WebTags { HttpMethod = "GET", HttpUserAgent = "ua", HttpClientIp = "1.2.3.4", NetworkClientIp = "5.6.7.8" };

            var keys = GetTagsSnapshot(tags, openTelemetrySemanticsEnabled).Select(x => x.Key).ToList();

            keys.Should().Contain(expectedKey).And.NotContain(unexpectedKey);

            // the other three aliases follow the same flag
            var otelKeys = new[] { Tags.UserAgentOriginal, Tags.ClientAddress, Tags.NetworkPeerAddress };
            var datadogKeys = new[] { Tags.HttpUserAgent, Tags.HttpClientIp, Tags.NetworkClientIp };
            var expected = openTelemetrySemanticsEnabled ? otelKeys : datadogKeys;
            var unexpected = openTelemetrySemanticsEnabled ? datadogKeys : otelKeys;

            keys.Should().Contain(expected).And.NotContain(unexpected);
        }

        [Fact]
        public void WebTagsOtelOnlyTagsAreEmittedUnderTheirOwnName()
        {
            // These have no Datadog equivalent, so they are emitted under the same name in both
            // modes. The instrumentation only populates them when OTel semantics are enabled.
            var tags = new WebTags
            {
                UrlScheme = "https",
                UrlPath = "/api/value",
                UrlQuery = "q=1",
                ServerAddress = "example.com",
                ServerPort = 8443,
                HttpRequestMethodOriginal = "GeT",
            };

            foreach (var openTelemetrySemanticsEnabled in new[] { false, true })
            {
                GetTagsSnapshot(tags, openTelemetrySemanticsEnabled)
                   .Should()
                   .Contain(
                    [
                        new KeyValuePair<string, string>(Tags.UrlScheme, "https"),
                        new KeyValuePair<string, string>(Tags.UrlPath, "/api/value"),
                        new KeyValuePair<string, string>(Tags.UrlQuery, "q=1"),
                        new KeyValuePair<string, string>(Tags.ServerAddress, "example.com"),
                        new KeyValuePair<string, string>(Tags.ServerPort, "8443"),
                        new KeyValuePair<string, string>(Tags.HttpRequestMethodOriginal, "GeT"),
                    ]);
            }
        }

        [Fact]
        public void StronglyTypedStatusCodeAliasCanBeClearedByEitherName()
        {
            var tags = new WebTags();

            tags.SetTag(Tags.HttpStatusCode, "203");
            tags.SetTag(Tags.HttpResponseStatusCode, null);
            tags.GetTag(Tags.HttpStatusCode).Should().BeNull();
            tags.GetTag(Tags.HttpResponseStatusCode).Should().BeNull();

            tags.SetTag(Tags.HttpResponseStatusCode, "204");
            tags.SetTag(Tags.HttpStatusCode, null);
            tags.GetTag(Tags.HttpStatusCode).Should().BeNull();
            tags.GetTag(Tags.HttpResponseStatusCode).Should().BeNull();
        }

        [Theory]
        [InlineData(false, Tags.HttpMethod, Tags.HttpUrl, Tags.OutHost)]
        [InlineData(true, Tags.HttpRequestMethod, Tags.UrlFull, Tags.ServerAddress)]
        public void HttpClientTagAliasesEnumerateSelectedNames(bool openTelemetrySemanticsEnabled, string methodKey, string urlKey, string hostKey)
        {
            const string url = "http://localhost/api";
            var tags = new HttpTags { HttpMethod = "GET", HttpUrl = url, Host = "localhost" };

            var snapshot = GetTagsSnapshot(tags, openTelemetrySemanticsEnabled);

            snapshot.Should().Contain(
            [
                new KeyValuePair<string, string>(methodKey, "GET"),
                new KeyValuePair<string, string>(urlKey, url),
                new KeyValuePair<string, string>(hostKey, "localhost"),
            ]);

            // the aliases are mutually exclusive, so only one name is reported for each concept
            var aliases = new[] { Tags.HttpMethod, Tags.HttpRequestMethod, Tags.HttpUrl, Tags.UrlFull, Tags.OutHost, Tags.ServerAddress };
            snapshot.Select(x => x.Key)
                    .Where(aliases.Contains)
                    .Should()
                    .BeEquivalentTo(new[] { methodKey, urlKey, hostKey });
        }

        [Fact]
        public void HttpClientTagAliasesCanBeReadAndWrittenByEitherName()
        {
            var tags = new HttpTags();

            tags.SetTag(Tags.HttpMethod, "GET");
            tags.GetTag(Tags.HttpRequestMethod).Should().Be("GET");
            tags.SetTag(Tags.HttpRequestMethod, "POST");
            tags.GetTag(Tags.HttpMethod).Should().Be("POST");
            tags.HttpMethod.Should().Be("POST");

            tags.SetTag(Tags.HttpUrl, "http://localhost/1");
            tags.GetTag(Tags.UrlFull).Should().Be("http://localhost/1");
            tags.SetTag(Tags.UrlFull, "http://localhost/2");
            tags.GetTag(Tags.HttpUrl).Should().Be("http://localhost/2");
            tags.HttpUrl.Should().Be("http://localhost/2");

            tags.SetTag(Tags.OutHost, "host1");
            tags.GetTag(Tags.ServerAddress).Should().Be("host1");
            tags.SetTag(Tags.ServerAddress, "host2");
            tags.GetTag(Tags.OutHost).Should().Be("host2");
            tags.Host.Should().Be("host2");
        }

        [Fact]
        public void ServerPortIsOnlyReportedWhenSet()
        {
            var tags = new HttpTags();

            GetTagsSnapshot(tags, openTelemetrySemanticsEnabled: true)
               .Select(x => x.Key)
               .Should()
               .NotContain(Tags.ServerPort);

            tags.ServerPort = 8080;

            GetTagsSnapshot(tags, openTelemetrySemanticsEnabled: true)
               .Should()
               .Contain(new KeyValuePair<string, string>(Tags.ServerPort, "8080"));
        }

        [Fact]
        public void GetTag_GetMetric_ReturnUpdatedValues()
        {
            var tags = new TagsList();
            var scope = _tracer.StartActiveInternal("root", tags: tags);
            var span = scope.Span;

            const int customTagCount = 15;
            SetupForSerializationTest(span, customTagCount);

            span.Context.TraceContext.Environment.Should().Be("Overridden Environment");
            span.GetTag(Tags.Env).Should().Be("Overridden Environment");
            span.GetMetric(Metrics.SamplingLimitDecision).Should().Be(0.75);

            for (int i = 0; i < customTagCount; i++)
            {
                var key = i.ToString();

                span.GetTag(key).Should().Be(key);
                span.GetMetric(key).Should().Be(i);
            }
        }

        [Fact]
        public void CheckProperties()
        {
            Action<ITags, string, string> setTag = (tagsList, name, value) => tagsList.SetTag(name, value);
            Func<ITags, string, string> getTag = (tagsList, name) => tagsList.GetTag(name);
            Action<ITags, string, double?> setMetric = (tagsList, name, value) => tagsList.SetMetric(name, value);
            Func<ITags, string, double?> getMetric = (tagsList, name) => tagsList.GetMetric(name);

            var assemblies = new[] { typeof(TagsList).Assembly, typeof(SqlTags).Assembly }.Distinct();

            foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
            {
                if (!typeof(TagsList).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.IsInterface || type.IsAbstract)
                {
                    continue;
                }

                var random = new Random();

                ValidateProperties(type, setTag, getTag, () => Guid.NewGuid().ToString());
                ValidateProperties(type, setMetric, getMetric, () => random.NextDouble());
            }
        }

        [Fact]
        public async Task Serialization_RootSpan()
        {
            const int customTagCount = 15;
            string hexStringTraceId;
            using (var scope = _tracer.StartActiveInternal("root"))
            {
                SetupForSerializationTest(scope.Span, customTagCount);
                hexStringTraceId = HexString.ToHexString(scope.Span.TraceId128.Upper);
            }

            await _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();
            var deserializedSpan = traceChunks.Single().Single();

            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
            deserializedSpan.Tags.Should().Contain(Tags.RuntimeId, Tracer.RuntimeId);
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, SamplingMechanism.Default);
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Should().ContainKey(Tags.ProcessTags);
            deserializedSpan.Tags.Should().HaveCount(customTagCount + 6);

            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingPriority, 1);
            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().Contain(Metrics.TopLevelSpan, 1);
            deserializedSpan.Metrics.Should().Contain(Metrics.ProcessId, DomainMetadata.Instance.ProcessId);
            deserializedSpan.Metrics.Should().ContainKey(Metrics.TracesKeepRate);
            deserializedSpan.Metrics.Should().HaveCount(customTagCount + 5);

            for (int i = 0; i < customTagCount; i++)
            {
                var key = i.ToString();

                deserializedSpan.Tags.Should().Contain(key, key);
                deserializedSpan.Metrics.Should().Contain(key, i);
            }
        }

        [Fact]
        public async Task Serialization_ServiceEntrySpan()
        {
            const int customTagCount = 15;
            string hexStringTraceId;

            using (_ = _tracer.StartActiveInternal("root", serviceName: "service1"))
            {
                using (var childScope = _tracer.StartActiveInternal("child", serviceName: "service2"))
                {
                    SetupForSerializationTest(childScope.Span, customTagCount);
                    hexStringTraceId = HexString.ToHexString(childScope.Span.TraceId128.Upper);
                }
            }

            await _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();
            var deserializedSpan = traceChunks.Single().Single(s => s.ParentId > 0);

            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
            deserializedSpan.Tags.Should().Contain(Tags.RuntimeId, Tracer.RuntimeId);
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, "-0"); // the child span is serialized first in the trace chunk, and this tag is added to the first span
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Should().ContainKey(Tags.BaseService);
            deserializedSpan.Tags[Tags.BaseService].Should().Be(_tracer.DefaultServiceName);
            deserializedSpan.Tags.Should().ContainKey(Tags.ProcessTags);
            deserializedSpan.Tags.Should().HaveCount(customTagCount + 7);

            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().Contain(Metrics.TopLevelSpan, 1);
            deserializedSpan.Metrics.Should().HaveCount(customTagCount + 2);

            for (int i = 0; i < customTagCount; i++)
            {
                var key = i.ToString();

                deserializedSpan.Tags.Should().Contain(key, key);
                deserializedSpan.Metrics.Should().Contain(key, i);
            }
        }

        [Fact]
        public async Task Serialization_ChildSpan()
        {
            const int customTagCount = 15;
            string hexStringTraceId;

            using (_ = _tracer.StartActiveInternal("root", serviceName: "service1"))
            {
                using (var childScope = _tracer.StartActiveInternal("child", serviceName: "service1"))
                {
                    SetupForSerializationTest(childScope.Span, customTagCount);
                    hexStringTraceId = HexString.ToHexString(childScope.Span.TraceId128.Upper);
                }
            }

            await _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();
            var deserializedSpan = traceChunks.Single().Single(s => s.ParentId > 0);

            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, "-0"); // the child span is serialized first in the trace chunk, and this tag is added to the first span
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Should().ContainKey(Tags.BaseService);
            deserializedSpan.Tags[Tags.BaseService].Should().Be(_tracer.DefaultServiceName);
            deserializedSpan.Tags.Should().ContainKey(Tags.ProcessTags);
            deserializedSpan.Tags.Should().HaveCount(customTagCount + 6);

            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().HaveCount(customTagCount + 1);

            for (int i = 0; i < customTagCount; i++)
            {
                var key = i.ToString();

                deserializedSpan.Tags.Should().Contain(key, key);
                deserializedSpan.Metrics.Should().Contain(key, i);
            }
        }

        [Fact]
        public async Task Serialization_SettingReadOnlyProperty()
        {
            var tags = new WebTags();
            using (var scope = _tracer.StartActiveInternal("root", serviceName: "service1", tags: tags))
            {
                // Read only property, so shouldn't be able to set it
                tags.SetTag(Trace.Tags.SpanKind, SpanKinds.Client);
            }

            await _tracer.FlushAsync();

            var traceChunks = _testApi.Wait(TimeSpan.FromSeconds(20));

            var deserializedSpan = traceChunks.Should().ContainSingle().Which.Should().ContainSingle().Subject;
            deserializedSpan.Tags.Should().Contain(Tags.SpanKind, SpanKinds.Server);
        }

        [Fact]
        public async Task Serialize_LanguageTag_ManualInstrumentation()
        {
            using (var scope = _tracer.StartActive("root"))
            {
            }

            await _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();
            var deserializedSpan = traceChunks.Single().Single();

            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
        }

        [Theory]
        [InlineData(SpanKinds.Client)]
        [InlineData(SpanKinds.Server)]
        [InlineData(SpanKinds.Producer)]
        [InlineData(SpanKinds.Consumer)]
        [InlineData(SpanKinds.Internal)]
        [InlineData("other")]
        public async Task Serialize_LanguageTag_AutomaticInstrumentation(string spanKind)
        {
            const int customTagCount = 15;

            var tags = new Mock<InstrumentationTags>();
            tags.Setup(t => t.SpanKind).Returns(spanKind);

            using (var scope = _tracer.StartActiveInternal("root", tags: tags.Object))
            {
                SetupForSerializationTest(scope.Span, customTagCount);
            }

            await _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();
            var deserializedSpan = traceChunks.Single().Single();
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
        }

        private static void SetupForSerializationTest(Span span, int customTagCount)
        {
            // The span has 1 "common" tag and 15 additional tags (and same number of metrics)
            // Those numbers are picked to test the variable-size header of MessagePack
            // The header is resized when there are 16 or more elements in the collection
            // Neither common or additional tags have enough elements, but put together they will cause to use a bigger header
            span.Context.TraceContext.Environment = "Test";

            // Override the properties
            span.SetTag(Tags.Env, "Overridden Environment");
            span.SetMetric(Metrics.SamplingLimitDecision, 0.75);

            for (int i = 0; i < customTagCount; i++)
            {
                var key = i.ToString();

                span.SetTag(key, key);
                span.SetMetric(key, i);
            }
        }

        private static void ValidateProperties<T>(Type type, Action<ITags, string, T> setTagValue, Func<ITags, string, T> getTagValue, Func<T> valueGenerator)
        {
            var instance = (ITags)Activator.CreateInstance(type);
            var isTag = typeof(T) == typeof(string);

            var allProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                    .Where(p => p.PropertyType == typeof(T));

            var propertyAndTagName = allProperties
                                    .Select(property =>
                                     {
                                         var name = isTag
                                                        ? property.GetCustomAttribute<TagAttribute>()?.TagName
                                                        : property.GetCustomAttribute<MetricAttribute>()?.MetricName;
                                         return (property, tagOrMetric: name);
                                     })
                                    .ToArray();

            if (isTag && type != typeof(TagsList))
            {
                // skip this for CommonTags because it is the only type without any string tags
                propertyAndTagName
                   .Should()
                   .OnlyContain(x => !string.IsNullOrEmpty(x.tagOrMetric));
            }

            var writeableProperties = propertyAndTagName.Where(p => p.property.CanWrite).ToArray();
            var readonlyProperties = propertyAndTagName.Where(p => !p.property.CanWrite).ToArray();

            // ---------- Test read-write properties
            var testValues = Enumerable.Range(0, writeableProperties.Length).Select(_ => valueGenerator()).ToArray();

            for (var i = 0; i < writeableProperties.Length; i++)
            {
                var (property, tagName) = writeableProperties[i];
                var testValue = testValues[i];

                setTagValue(instance, tagName, testValue);

                property.GetValue(instance).Should().Be(testValue, $"Getter and setter mismatch for tag {property.Name} of type {type.Name}");

                var actualValue = getTagValue(instance, tagName);

                actualValue.Should().Be(testValue, $"Getter and setter mismatch for tag {property.Name} of type {type.Name}");
            }

            // Check that all read/write properties were mapped
            var remainingValues = new HashSet<T>(testValues);

            foreach (var property in writeableProperties)
            {
                remainingValues.Remove((T)property.property.GetValue(instance))
                               .Should()
                               .BeTrue($"Property {property.property.Name} of type {type.Name} is not mapped");
            }

            // ---------- Test readonly properties
            remainingValues = new HashSet<T>(readonlyProperties.Select(p => (T)p.property.GetValue(instance)));

            foreach (var propertyAndTag in readonlyProperties)
            {
                var tagName = propertyAndTag.tagOrMetric;
                var tagValue = getTagValue(instance, tagName);

                remainingValues.Remove(tagValue)
                               .Should()
                               .BeTrue($"Property {propertyAndTag.property.Name} of type {type.Name} is not mapped");
            }
        }

        private static List<KeyValuePair<string, string>> GetTagsSnapshot(TagsList tags, bool openTelemetrySemanticsEnabled = false)
        {
            var result = new List<KeyValuePair<string, string>>();
            var processor = new TagCollectorProcessor(result);
            tags.EnumerateTags(ref processor, openTelemetrySemanticsEnabled);
            return result;
        }

        private static object GetBackingTagsList(TagsList tags)
        {
            var field = typeof(TagsList).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field.GetValue(tags);
        }

        private readonly struct TagCollectorProcessor : IItemProcessor<string>, IItemProcessor<int>
        {
            private readonly List<KeyValuePair<string, string>> _items;

            public TagCollectorProcessor(List<KeyValuePair<string, string>> items)
            {
                _items = items;
            }

            public void Process(TagItem<string> item)
            {
                _items.Add(new(item.Key, item.Value));
            }

            public void Process(TagItem<int> item)
            {
                _items.Add(new(item.Key, item.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
