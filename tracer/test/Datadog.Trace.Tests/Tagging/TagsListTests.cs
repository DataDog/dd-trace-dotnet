// <copyright file="TagsListTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class TagsListTests
    {
        [Fact]
        public void GetTag_GetMetric_ReturnUpdatedValues()
        {
            var tags = new CommonTags();
            var span = new Span(new SpanContext(42, 41), DateTimeOffset.UtcNow, tags);

            tags.Environment = "Test";
            tags.SamplingLimitDecision = 0.5;

            // Override the properties
            span.SetTag(Tags.Env, "Overridden Environment");
            span.SetMetric(Metrics.SamplingLimitDecision, 0.75);

            for (int i = 0; i < 15; i++)
            {
                span.SetTag(i.ToString(), i.ToString());
            }

            for (int i = 0; i < 15; i++)
            {
                span.SetMetric(i.ToString(), i);
            }

            Assert.Equal("Overridden Environment", span.GetTag(Tags.Env));
            Assert.Equal(0.75, span.GetMetric(Metrics.SamplingLimitDecision));

            for (int i = 0; i < 15; i++)
            {
                Assert.Equal(i.ToString(), span.GetTag(i.ToString()));
                Assert.Equal((double)i, span.GetMetric(i.ToString()));
            }
        }

        [Fact]
        public void CheckProperties()
        {
            var assemblies = new[] { typeof(TagsList).Assembly, typeof(SqlTags).Assembly };

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

                Action<ITags, string, string> setTag = (tagsList, name, value) => tagsList.SetTag(name, value);
                Func<ITags, string, string> getTag = (tagsList, name) => tagsList.GetTag(name);
                Action<ITags, string, double?> setMetric = (tagsList, name, value) => tagsList.SetMetric(name, value);
                Func<ITags, string, double?> getMetric = (tagsList, name) => tagsList.GetMetric(name);

                ValidateProperties(type, setTag, getTag, () => Guid.NewGuid().ToString());
                ValidateProperties(type, setMetric, getMetric, () => random.NextDouble());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Serialization(bool topLevelSpan)
        {
            var tags = new CommonTags();

            Span span;

            if (topLevelSpan)
            {
                span = new Span(new SpanContext(42, 41), DateTimeOffset.UtcNow, tags);
            }
            else
            {
                // Assign a parent to prevent the span from being considered as top-level
                var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
                var parent = new SpanContext(42, 41);
                span = new Span(new SpanContext(parent, traceContext, null), DateTimeOffset.UtcNow, tags);
            }

            // The span has 1 "common" tag and 15 additional tags (and same number of metrics)
            // Those numbers are picked to test the variable-size header of MessagePack
            // The header is resized when there are 16 or more elements in the collection
            // Neither common or additional tags have enough elements, but put together they will cause to use a bigger header
            tags.Environment = "Test";
            tags.SamplingLimitDecision = 0.5;

            // Override the properties
            span.SetTag(Tags.Env, "Overridden Environment");
            span.SetMetric(Metrics.SamplingLimitDecision, 0.75);

            for (int i = 0; i < 15; i++)
            {
                span.SetTag(i.ToString(), i.ToString());
            }

            for (int i = 0; i < 15; i++)
            {
                span.SetMetric(i.ToString(), i);
            }

            var buffer = new byte[0];

            var resolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);
            MessagePackSerializer.Serialize(ref buffer, 0, span, resolver);

            var deserializedSpan = MessagePack.MessagePackSerializer.Deserialize<FakeSpan>(buffer);

            // For top-level spans, there is one tag added during serialization
            Assert.Equal(topLevelSpan ? 17 : 16, deserializedSpan.Tags.Count);

            // For top-level spans, there is one metric added during serialization
            Assert.Equal(topLevelSpan ? 17 : 16, deserializedSpan.Metrics.Count);

            Assert.Equal("Overridden Environment", deserializedSpan.Tags[Tags.Env]);
            Assert.Equal(0.75, deserializedSpan.Metrics[Metrics.SamplingLimitDecision]);

            for (int i = 0; i < 15; i++)
            {
                Assert.Equal(i.ToString(), deserializedSpan.Tags[i.ToString()]);
                Assert.Equal((double)i, deserializedSpan.Metrics[i.ToString()]);
            }

            if (topLevelSpan)
            {
                Assert.Equal(Tracer.RuntimeId, deserializedSpan.Tags[Tags.RuntimeId]);
                Assert.Equal(1.0, deserializedSpan.Metrics[Metrics.TopLevelSpan]);
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

            propertyAndTagName
               .Should()
               .OnlyContain(x => !string.IsNullOrEmpty(x.tagOrMetric));

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

        [MessagePack.MessagePackObject]
        public struct FakeSpan
        {
            [MessagePack.Key("trace_id")]
            public ulong TraceId { get; set; }

            [MessagePack.Key("span_id")]
            public ulong SpanId { get; set; }

            [MessagePack.Key("name")]
            public string Name { get; set; }

            [MessagePack.Key("resource")]
            public string Resource { get; set; }

            [MessagePack.Key("service")]
            public string Service { get; set; }

            [MessagePack.Key("type")]
            public string Type { get; set; }

            [MessagePack.Key("start")]
            public long Start { get; set; }

            [MessagePack.Key("duration")]
            public long Duration { get; set; }

            [MessagePack.Key("parent_id")]
            public ulong? ParentId { get; set; }

            [MessagePack.Key("error")]
            public byte Error { get; set; }

            [MessagePack.Key("meta")]
            public Dictionary<string, string> Tags { get; set; }

            [MessagePack.Key("metrics")]
            public Dictionary<string, double> Metrics { get; set; }
        }
    }
}
