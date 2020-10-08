using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Tagging;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class TagsListTests
    {
        [Fact]
        public void CheckProperties()
        {
            var assembly = typeof(TagsList).Assembly;

            foreach (var type in assembly.GetTypes())
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

        private void ValidateProperties<T>(Type type, string methodName, Func<T> valueGenerator)
        {
            var instance = (ITags)Activator.CreateInstance(type);

            var tags = (IProperty<T>[])type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(T))
                .ToArray();

            Assert.Equal(properties.Length, tags.Length);

            var testValues = Enumerable.Range(0, tags.Length).Select(_ => valueGenerator()).ToArray();

            // Check for each tag that the getter and the setter are mapped on the same property
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];

                tag.Setter(instance, testValues[i]);

                Assert.True(testValues[i].Equals(tag.Getter(instance)), $"Getter and setter mismatch for tag {tag.Key} of type {type.Name}");
            }

            // Check that all properties were mapped
            var remainingValues = new HashSet<T>(testValues);

            foreach (var property in properties)
            {
                Assert.True(remainingValues.Remove((T)property.GetValue(instance)), $"Property {property.Name} of type {type.Name} is not mapped");
            }
        }
    }
}
