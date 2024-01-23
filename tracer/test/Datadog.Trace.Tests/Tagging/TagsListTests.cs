// <copyright file="TagsListTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class TagsListTests
    {
        private readonly Tracer _tracer;
        private readonly MockApi _testApi;

        public TagsListTests()
        {
            var settings = new TracerSettings();
            _testApi = new MockApi();
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null, automaticFlush: false);
            _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
        }

        [Fact]
        public void GetTag_GetMetric_ReturnUpdatedValues()
        {
            var tags = new CommonTags();
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
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, SamplingMechanism.GetTagValue(SamplingMechanism.Default));
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Should().HaveCount(customTagCount + 5);

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
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, "-0"); // the child span is serialized first in the trace chunk
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Count.Should().Be(customTagCount + 5);

            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().Contain(Metrics.TopLevelSpan, 1);
            deserializedSpan.Metrics.Count.Should().Be(customTagCount + 2);

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
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.DecisionMaker, "-0"); // the child span is serialize first in the trace chunk
            deserializedSpan.Tags.Should().Contain(Tags.Propagated.TraceIdUpper, hexStringTraceId);
            deserializedSpan.Tags.Count.Should().Be(customTagCount + 4);

            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Count.Should().Be(customTagCount + 1);

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
            var tags = (CommonTags)span.Tags;
            tags.SamplingLimitDecision = 0.5;

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

            if (isTag && type != typeof(CommonTags))
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
    }
}
