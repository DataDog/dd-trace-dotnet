using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Xunit;
// ReSharper disable UnusedMember.Global

namespace Datadog.Trace.Tests.Debugger
{
    public class TypeFinderTests
    {
        public TypeFinderTests()
        {
            // Ensure TypeFinder.Instance is initialized
            TypeFinder.EnsureInitialized();
        }

        [Fact]
        public void FindTypes_NonGenericType_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("System.String").ToList();

            Assert.Single(types);
            Assert.Equal(typeof(string), types[0]);
        }

        [Fact]
        public void FindTypes_GenericType_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("System.Collections.Generic.List<>").ToList();

            Assert.Single(types);
            Assert.Equal(typeof(List<>), types[0]);
        }

        [Fact]
        public void FindTypes_PartialName_ReturnsCorrectTypes()
        {
            var types = TypeFinder.Instance.FindTypes("List<>").ToList();

            Assert.Contains(types, t => t == typeof(List<>));
        }

        [Fact]
        public void TypeFinder_UsedMultipleTimes_InitializedOnlyOnce()
        {
            // This test ensures that using TypeFinder.Instance multiple times doesn't cause issues
            var types1 = TypeFinder.Instance.FindTypes("System.String");
            var types2 = TypeFinder.Instance.FindTypes("System.Int32");
            var types3 = TypeFinder.Instance.FindTypes("System.Collections.Generic.List<>");

            Assert.NotEmpty(types1);
            Assert.NotEmpty(types2);
            Assert.NotEmpty(types3);
        }

        [Fact]
        public void FindTypes_PartialNamespace_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("Collections.Generic.List<>").ToList();

            Assert.Contains(types, t => t == typeof(List<>));
        }

        [Fact]
        public void FindTypes_FullyQualifiedName_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("System.Collections.Generic.List<>").ToList();

            Assert.Contains(types, t => t == typeof(List<>));
        }

        [Fact]
        public void FindTypes_NonExistentType_ReturnsEmptyCollection()
        {
            var types = TypeFinder.Instance.FindTypes("NonExistentType");

            Assert.Empty(types);
        }

        [Fact]
        public void FindTypes_MultipleMatchingTypes_ReturnsAllMatches()
        {
            // This test assumes there are multiple types named "Attribute" in different namespaces
            var types = TypeFinder.Instance.FindTypes("Attribute").ToList();

            Assert.True(types.Count > 1);
            Assert.Contains(types, t => t == typeof(System.Attribute));
        }

        [Fact]
        public void FindTypes_CaseInsensitive_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("system.string").ToList();

            Assert.Single(types);
            Assert.Equal(typeof(string), types[0]);
        }

        [Fact]
        public void FindTypes_NestedType_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("System.Environment+SpecialFolder").ToList();

            Assert.Single(types);
            Assert.Equal(typeof(Environment.SpecialFolder), types[0]);
        }

        [Fact]
        public void FindTypes_GenericTypeWithConstraints_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("System.Collections.Generic.Dictionary<,>").ToList();

            Assert.Contains(types, t => t == typeof(Dictionary<,>));
        }

        [Fact]
        public void FindTypes_TypeFromCurrentAssembly_ReturnsCorrectType()
        {
            var types = TypeFinder.Instance.FindTypes("TypeFinderTests").ToList();

            Assert.Single(types);
            Assert.Equal(GetType(), types[0]);
        }

        [Fact]
        public void EnsureInitialized_CalledMultipleTimes_DoesNotThrowException()
        {
            for (int i = 0; i < 5; i++)
            {
                TypeFinder.EnsureInitialized();
            }

            // If we reach here without exception, the test passes
        }

        [Fact]
        public void FindTypes_AfterLoadingNewAssembly_FindsNewTypes()
        {
            // This test simulates loading a new assembly and finding types from it
            // Note: This test might not work in all environments due to assembly loading restrictions

            string assemblyPath = typeof(Xunit.Assert).Assembly.Location;
            Assembly newAssembly = Assembly.LoadFrom(assemblyPath);

            var types = TypeFinder.Instance.FindTypes("Xunit.Assert").ToList();

            Assert.Contains(types, t => t.FullName == "Xunit.Assert");
        }
    }

    // Helper classes for testing
#pragma warning disable SA1402 // File may only contain a single type
    public class GenericClass<T>
#pragma warning restore SA1402 // File may only contain a single type
    {

    }

#pragma warning disable SA1402 // File may only contain a single type
    public class ConstrainedGenericClass<T>
#pragma warning restore SA1402 // File may only contain a single type
        where T : class
    {

    }

#pragma warning disable SA1402 // File may only contain a single type
    public class OuterClass
#pragma warning restore SA1402 // File may only contain a single type
    {
        public class NestedClass
        {

        }
    }
}
