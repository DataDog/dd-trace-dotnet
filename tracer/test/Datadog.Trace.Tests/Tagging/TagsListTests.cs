// <copyright file="TagsListTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
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

        [Fact]
        public void GetTag_GetMetric_ReturnUpdatedValues()
        {
            var tags = new TagsList();
            var scope = _tracer.StartActiveInternal("root", tags: tags);
            var span = (Span)scope.Span;

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
                SetupForSerializationTest((Span)scope.Span, customTagCount);
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
                    SetupForSerializationTest((Span)childScope.Span, customTagCount);
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
                    SetupForSerializationTest((Span)childScope.Span, customTagCount);
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
                SetupForSerializationTest((Span)scope.Span, customTagCount);
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

        private static List<KeyValuePair<string, string>> GetTagsSnapshot(TagsList tags)
        {
            var result = new List<KeyValuePair<string, string>>();
            var processor = new TagCollectorProcessor(result);
            tags.EnumerateTags(ref processor);
            return result;
        }

        private static object GetBackingTagsList(TagsList tags)
        {
            var field = typeof(TagsList).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field.GetValue(tags);
        }

        private readonly struct TagCollectorProcessor : IItemProcessor<string>
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
        }
    }
}
