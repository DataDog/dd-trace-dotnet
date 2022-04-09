// <copyright file="TagsListTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent.MessagePack;
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

        [Fact]
        public void Serialization_RootSpan()
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);
            var span = new Span(new SpanContext(SpanContext.None, traceContext, "service1"), start: null);
            traceContext.AddSpan(span);

            const int customTagCount = 15;
            SetupForSerializationTest(span, customTagCount);
            var deserializedSpan = SerializeSpan(span);

            deserializedSpan.Tags.Count.Should().Be(customTagCount + 3);
            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
            deserializedSpan.Tags.Should().Contain(Tags.RuntimeId, Tracer.RuntimeId);

            deserializedSpan.Metrics.Count.Should().Be(customTagCount + 3);
            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().Contain(Metrics.TopLevelSpan, 1);
            deserializedSpan.Metrics.Should().Contain(Metrics.ProcessId, DomainMetadata.Instance.ProcessId);

            for (int i = 0; i < customTagCount; i++)
            {
                deserializedSpan.Tags.Should().Contain(i.ToString(), i.ToString());
                deserializedSpan.Metrics.Should().Contain(i.ToString(), i);
            }
        }

        [Fact]
        public void Serialization_ServiceEntrySpan()
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);

            var rootSpan = new Span(new SpanContext(SpanContext.None, traceContext, "service1"), start: null);
            traceContext.AddSpan(rootSpan);
            var childSpan = new Span(new SpanContext(rootSpan.Context, traceContext, "service2"), start: null);
            traceContext.AddSpan(childSpan);

            const int customTagCount = 15;
            SetupForSerializationTest(childSpan, customTagCount);
            var deserializedSpan = SerializeSpan(childSpan);

            deserializedSpan.Tags.Count.Should().Be(customTagCount + 3);
            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
            deserializedSpan.Tags.Should().Contain(Tags.RuntimeId, Tracer.RuntimeId);

            deserializedSpan.Metrics.Count.Should().Be(customTagCount + 2);
            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);
            deserializedSpan.Metrics.Should().Contain(Metrics.TopLevelSpan, 1);

            for (int i = 0; i < customTagCount; i++)
            {
                deserializedSpan.Tags.Should().Contain(i.ToString(), i.ToString());
                deserializedSpan.Metrics.Should().Contain(i.ToString(), i);
            }
        }

        [Fact]
        public void Serialization_ChildSpan()
        {
            var tracer = new Mock<IDatadogTracer>();
            var traceContext = new TraceContext(tracer.Object);

            var rootSpan = new Span(new SpanContext(SpanContext.None, traceContext, "service1"), start: null);
            traceContext.AddSpan(rootSpan);
            var childSpan = new Span(new SpanContext(rootSpan.Context, traceContext, "service1"), start: null);
            traceContext.AddSpan(childSpan);

            const int customTagCount = 15;
            SetupForSerializationTest(childSpan, customTagCount);
            var deserializedSpan = SerializeSpan(childSpan);

            deserializedSpan.Tags.Count.Should().Be(customTagCount + 2);
            deserializedSpan.Tags.Should().Contain(Tags.Env, "Overridden Environment");
            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);

            deserializedSpan.Metrics.Count.Should().Be(customTagCount + 1);
            deserializedSpan.Metrics.Should().Contain(Metrics.SamplingLimitDecision, 0.75);

            for (int i = 0; i < customTagCount; i++)
            {
                deserializedSpan.Tags.Should().Contain(i.ToString(), i.ToString());
                deserializedSpan.Metrics.Should().Contain(i.ToString(), i);
            }
        }

        [Fact]
        public void Serialize_LanguageTag_Manual()
        {
            // manual spans use CommonTags
            var span = new Span(new SpanContext(42, 41), DateTimeOffset.UtcNow);
            var deserializedSpan = SerializeSpan(span);

            deserializedSpan.Tags.Should().Contain(Tags.Language, TracerConstants.Language);
        }

        [Theory]
        [InlineData(SpanKinds.Client, null)]
        [InlineData(SpanKinds.Server, TracerConstants.Language)]
        [InlineData(SpanKinds.Producer, null)]
        [InlineData(SpanKinds.Consumer, TracerConstants.Language)]
        [InlineData("other", TracerConstants.Language)]
        public void Serialize_LanguageTag_Automatic(string spanKind, string expectedLanguage)
        {
            var tags = new Mock<InstrumentationTags>();
            tags.Setup(t => t.SpanKind).Returns(spanKind);

            var span = new Span(new SpanContext(42, 41), DateTimeOffset.UtcNow, tags.Object);
            var deserializedSpan = SerializeSpan(span);

            if (expectedLanguage == null)
            {
                deserializedSpan.Tags.Should().NotContainKey(Tags.Language);
            }
            else
            {
                deserializedSpan.Tags.Should().Contain(Tags.Language, expectedLanguage);
            }
        }

        private static MockSpan SerializeSpan(Span span)
        {
            var buffer = new byte[0];

            // use vendored MessagePack to serialize
            var resolver = SpanFormatterResolver.Instance;
            Vendors.MessagePack.MessagePackSerializer.Serialize(ref buffer, 0, span, resolver);

            // use nuget MessagePack to deserialize
            return global::MessagePack.MessagePackSerializer.Deserialize<MockSpan>(buffer);
        }

        private static void SetupForSerializationTest(Span span, int customTagCount)
        {
            // The span has 1 "common" tag and 15 additional tags (and same number of metrics)
            // Those numbers are picked to test the variable-size header of MessagePack
            // The header is resized when there are 16 or more elements in the collection
            // Neither common or additional tags have enough elements, but put together they will cause to use a bigger header
            var tags = (CommonTags)span.Tags;
            tags.Environment = "Test";
            tags.SamplingLimitDecision = 0.5;

            // Override the properties
            span.SetTag(Tags.Env, "Overridden Environment");
            span.SetMetric(Metrics.SamplingLimitDecision, 0.75);

            for (int i = 0; i < customTagCount; i++)
            {
                span.SetTag(i.ToString(), i.ToString());
            }

            for (int i = 0; i < customTagCount; i++)
            {
                span.SetMetric(i.ToString(), i);
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
    }
}
