// <copyright file="DuckChainingNullableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckChainingNullableTests
    {
        [Fact]
        public void CanDuckChainNullableDirectly_WithNullNullable()
        {
            var instance = new GenericTestClass();
            var proxy = instance.DuckCast<GenericProxyStruct>();
        }

        [Fact]
        public void CanDuckChainNullableDirectlyWithNonNullNullable()
        {
            var instance = new GenericTestClass { MyTestStruct = new TestStruct() };
            var proxy = instance.DuckCast<GenericProxyStruct>();
        }

        [Fact]
        public void CanDuckChainNullableDirectly()
        {
            TestStruct? instance = new TestStruct();
            var proxy = DuckType.CreateCache<NullableProxyTestStruct>.CreateFrom(instance);
        }

        [Fact]
        public void CanDuckChainWithNullInstance()
        {
            var firstClass = new FirstClass();
            var firstClassProxy = firstClass.DuckCast<IFirstClass>();

            firstClass.Value.Should().BeNull();
            firstClassProxy.Value.Should().BeNull();
        }

        [Fact]
        public void CanDuckChainWithNullInstanceAndANullableDuckCopy()
        {
            var firstClass = new FirstClass();
            var firstClassCopy = firstClass.DuckCast<SFirstClass>();
            firstClassCopy.Value.Should().BeNull();

            firstClass.Value = new SecondClass
            {
                Name = "Hello World"
            };

            var firstClassCopy2 = firstClass.DuckCast<SFirstClass>();
            firstClassCopy2.Value.Should().NotBeNull();
            firstClassCopy2.Value!.Value.Name.Should().Be(firstClass.Value.Name);
        }

        // Proxies
        [DuckCopy]
        public struct GenericProxyStruct
        {
            public NullableProxyTestStruct MyTestStruct;
        }

        [DuckCopy]
        public struct NullableProxyTestStruct
        {
            public ProxyTestStruct Value;
        }

        [DuckCopy]
        public struct ProxyTestStruct
        {
        }

#pragma warning disable SA1201
        public interface IFirstClass
#pragma warning restore SA1201
        {
            ISecondClass Value { get; set; }
        }

        public interface ISecondClass
        {
            string Name { get; set; }
        }

        [DuckCopy]
        public struct SFirstClass
        {
            public SSecondClass? Value;
        }

        [DuckCopy]
        public struct SSecondClass
        {
            public string Name;
        }

        // Originals
        public struct TestStruct
        {
        }

        public class GenericTestClass
        {
            public TestStruct? MyTestStruct { get; set; }
        }

        public class FirstClass
        {
            public SecondClass Value { get; set; }
        }

        public class SecondClass
        {
            public string Name { get; set; }
        }
    }
}
