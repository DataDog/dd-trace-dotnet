using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.ClrProfiler.Integrations.AdoNet;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class TagsListTests
    {
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

                ValidateProperties<string>(type, "GetAdditionalTags", () => Guid.NewGuid().ToString());
                ValidateProperties<double?>(type, "GetAdditionalMetrics", () => random.NextDouble());
            }
        }

        [Fact]
        public void Serialization()
        {
            var tags = new CommonTags();
            var span = new Span(new SpanContext(42, 41), DateTimeOffset.UtcNow, tags);

            // The span has 1 "common" tag and 15 additional tags (and same number of metrics)
            // Those numbers are picked to test the variable-size header of MessagePack
            // The header is resized when there are 16 or more elements in the collection
            // Neither common or additional tags have enough elements, but put together they will cause to use a bigger header
            tags.Environment = "Test";
            tags.SamplingLimitDecision = 0.5;

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

            Assert.Equal(16, deserializedSpan.Tags.Count);
            Assert.Equal(16, deserializedSpan.Metrics.Count);

            Assert.Equal("Test", deserializedSpan.Tags[Tags.Env]);
            Assert.Equal(0.5, deserializedSpan.Metrics[Metrics.SamplingLimitDecision]);

            for (int i = 0; i < 15; i++)
            {
                Assert.Equal(i.ToString(), deserializedSpan.Tags[i.ToString()]);
                Assert.Equal((double)i, deserializedSpan.Metrics[i.ToString()]);
            }
        }

        private void ValidateProperties<T>(Type type, string methodName, Func<T> valueGenerator)
        {
            var instance = (ITags)Activator.CreateInstance(type, nonPublic: true);

            var allTags = (IProperty<T>[])type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);

            var tags = allTags.Where(t => !t.IsReadOnly).ToArray();
            var readonlyTags = allTags.Where(t => t.IsReadOnly).ToArray();

            var allProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(T))
                .ToArray();

            var properties = allProperties.Where(p => p.CanWrite).ToArray();
            var readonlyProperties = allProperties.Where(p => !p.CanWrite).ToArray();

            Assert.True(properties.Length == tags.Length, $"Mismatch between readonly properties and tags count for type {type}");
            Assert.True(readonlyProperties.Length == readonlyTags.Length, $"Mismatch between readonly properties and tags count for type {type}");

            // ---------- Test read-write properties
            var testValues = Enumerable.Range(0, tags.Length).Select(_ => valueGenerator()).ToArray();

            // Check for each tag that the getter and the setter are mapped on the same property
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];

                tag.Setter(instance, testValues[i]);

                Assert.True(testValues[i].Equals(tag.Getter(instance)), $"Getter and setter mismatch for tag {tag.Key} of type {type.Name}");
            }

            // Check that all read/write properties were mapped
            var remainingValues = new HashSet<T>(testValues);

            foreach (var property in properties)
            {
                Assert.True(remainingValues.Remove((T)property.GetValue(instance)), $"Property {property.Name} of type {type.Name} is not mapped");
            }

            // ---------- Test readonly properties
            remainingValues = new HashSet<T>(readonlyProperties.Select(p => (T)p.GetValue(instance)));

            foreach (var tag in readonlyTags)
            {
                Assert.True(remainingValues.Remove(tag.Getter(instance)), $"Tag {tag.Key} of type {type.Name} is not mapped");
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
