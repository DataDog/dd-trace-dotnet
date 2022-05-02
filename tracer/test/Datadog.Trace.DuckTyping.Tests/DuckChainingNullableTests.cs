// <copyright file="DuckChainingNullableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

        // Originals
        public struct TestStruct
        {
        }

        public class GenericTestClass
        {
            public TestStruct? MyTestStruct { get; set; }
        }
    }
}
