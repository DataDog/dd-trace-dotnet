// <copyright file="StructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping.Tests
{
    public class StructTests
    {
        [Fact]
        public void NonPublicStructCopyTest()
        {
            PrivateStruct instance = default;
            CopyStruct copy = instance.DuckCast<CopyStruct>();
            Assert.Equal(instance.Value, copy.Value);
        }

        [Fact]
        public void NonPublicStructInterfaceProxyTest()
        {
            PrivateStruct instance = default;
            IPrivateStruct proxy = instance.DuckCast<IPrivateStruct>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [Fact]
        public void NonPublicStructAbstractProxyTest()
        {
            PrivateStruct instance = default;
            AbstractPrivateProxy proxy = instance.DuckCast<AbstractPrivateProxy>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [Fact]
        public void NonPublicStructVirtualProxyTest()
        {
            PrivateStruct instance = default;
            VirtualPrivateProxy proxy = instance.DuckCast<VirtualPrivateProxy>();
            Assert.Equal(instance.Value, proxy.Value);
        }

        [DuckCopy]
        public struct CopyStruct
        {
            public string Value;
        }

        public interface IPrivateStruct
        {
            string Value { get; }
        }

        public abstract class AbstractPrivateProxy
        {
            public abstract string Value { get; }
        }

        public class VirtualPrivateProxy
        {
            public virtual string Value { get; }
        }

        private readonly struct PrivateStruct
        {
            public readonly string Value => "Hello World";
        }

        [Fact]
        public void DuckChainingStructInterfaceProxyTest()
        {
            PrivateDuckChainingTarget instance = new PrivateDuckChainingTarget();
            IPrivateDuckChainingTarget proxy = instance.DuckCast<IPrivateDuckChainingTarget>();
            Assert.Equal(instance.ChainingTestField.Name, proxy.ChainingTestField.Name);
            Assert.Equal(instance.ChainingTest.Name, proxy.ChainingTest.Name);
            Assert.Equal(instance.ChainingTestMethod().Name, proxy.ChainingTestMethod().Name);

            PublicDuckChainingTarget instance2 = new PublicDuckChainingTarget();
            IPrivateDuckChainingTarget proxy2 = instance2.DuckCast<IPrivateDuckChainingTarget>();
            Assert.Equal(instance2.ChainingTestField.Name, proxy2.ChainingTestField.Name);
            Assert.Equal(instance2.ChainingTest.Name, proxy2.ChainingTest.Name);
            Assert.Equal(instance2.ChainingTestMethod().Name, proxy2.ChainingTestMethod().Name);
        }

        public interface IPrivateDuckChainingTarget
        {
            [DuckField]
            IPrivateTarget ChainingTestField { get; }

            IPrivateTarget ChainingTest { get; }

            IPrivateTarget ChainingTestMethod();
        }

        public interface IPrivateTarget
        {
            [DuckField]
            public string Name { get; }
        }

        private class PrivateDuckChainingTarget
        {
#pragma warning disable SA1401 // Fields must be private
            public PrivateTarget ChainingTestField = new PrivateTarget { Name = "Hello World 1" };
#pragma warning restore SA1401 // Fields must be private

            public PrivateTarget ChainingTest => new PrivateTarget { Name = "Hello World 2" };

            public PrivateTarget ChainingTestMethod() => new PrivateTarget { Name = "Hello World 3" };
        }

        private struct PrivateTarget
        {
            public string Name;
        }

        public class PublicDuckChainingTarget
        {
#pragma warning disable SA1401 // Fields must be private
            public PublicTarget ChainingTestField = new PublicTarget { Name = "Hello World 1" };
#pragma warning restore SA1401 // Fields must be private

            public PublicTarget ChainingTest => new PublicTarget { Name = "Hello World 2" };

            public PublicTarget ChainingTestMethod() => new PublicTarget { Name = "Hello World 3" };
        }

        public struct PublicTarget
        {
            public string Name;
        }

        [Fact]
        public void NonPublicStructCopyFieldTest()
        {
            InternalFieldStruct instance = new InternalFieldStruct("InstanceValue");
            CopyFieldStruct copy = instance.DuckCast<CopyFieldStruct>();
            Assert.Equal(instance.Value, copy.Value);
            Assert.Equal(InternalFieldStruct.StaticValue, copy.StaticValue);
        }

        [DuckCopy]
        public struct CopyFieldStruct
        {
            [DuckField]
            public string Value;

            [DuckField]
            public string StaticValue;
        }

        public readonly struct InternalFieldStruct
        {
            public InternalFieldStruct(string value)
            {
                Value = value;
            }

            internal static readonly string StaticValue = "MyValue";
            internal readonly string Value;
        }

        // =====================================================================
        // Reproduction tests for DefaultModelBindingContext_SetResult_Integration
        // InvalidCastException (GitHub issue / error tracking)
        // =====================================================================

        /// <summary>
        /// When ValueProvider is typed as object in the DuckCopy struct, the DuckCopy
        /// succeeds even when the concrete IValueProvider does not implement IList.
        /// The safe cast to IList happens at the usage site instead.
        /// </summary>
        [Fact]
        public void DuckCopy_NonListValueProvider_SucceedsWithObjectField()
        {
            var instance = new FakeModelBindingContext(useNonListValueProvider: true);

            var result = instance.TryDuckCast<CopyOfModelBindingContext>(out var copy);

            Assert.True(result);
            Assert.NotNull(copy.ValueProvider);
            // ValueProvider is stored as object — it's NOT an IList
            Assert.False(copy.ValueProvider is System.Collections.IList);
        }

        /// <summary>
        /// When ValueProvider DOES implement IList (the standard CompositeValueProvider case),
        /// the DuckCopy succeeds and the value can be safely cast to IList at the usage site.
        /// </summary>
        [Fact]
        public void DuckCopy_CompositeValueProvider_SucceedsAndIsIList()
        {
            var instance = new FakeModelBindingContext(useNonListValueProvider: false);

            var result = instance.TryDuckCast<CopyOfModelBindingContext>(out var copy);

            Assert.True(result);
            Assert.Equal("Body", copy.BindingSource.Id);
            Assert.True(copy.ValueProvider is System.Collections.IList);
        }

        // --- Fake source types simulating ASP.NET Core model binding ---

        /// <summary>
        /// Simulates Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext
        /// </summary>
        public class FakeModelBindingContext
        {
            public FakeModelBindingContext(bool useNonListValueProvider = false)
            {
                BindingSource = new FakeBindingSource("Body");
                ValueProvider = useNonListValueProvider
                    ? (IFakeValueProvider)new SimpleValueProvider()     // does NOT implement IList
                    : (IFakeValueProvider)new CompositeValueProvider(); // implements IList
            }

            public FakeBindingSource BindingSource { get; set; }

            public IFakeValueProvider ValueProvider { get; set; }
        }

        /// <summary>
        /// Simulates Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource (a class, not a struct).
        /// </summary>
        public class FakeBindingSource
        {
            public FakeBindingSource(string id) => Id = id;

            public string Id { get; }
        }

        /// <summary>Simulates IValueProvider</summary>
        public interface IFakeValueProvider
        {
            bool ContainsPrefix(string prefix);
        }

        /// <summary>
        /// Simulates CompositeValueProvider which extends Collection&lt;IValueProvider&gt; (implements IList).
        /// </summary>
        public class CompositeValueProvider : System.Collections.ObjectModel.Collection<IFakeValueProvider>, IFakeValueProvider
        {
            public bool ContainsPrefix(string prefix) => false;
        }

        /// <summary>
        /// A simple IValueProvider that does NOT implement IList.
        /// This is the case that triggers the InvalidCastException.
        /// </summary>
        public class SimpleValueProvider : IFakeValueProvider
        {
            public bool ContainsPrefix(string prefix) => false;
        }

        // --- DuckCopy target types (mirrors tracer's duck types) ---

        [DuckCopy]
        public struct CopyOfModelBindingContext
        {
            public CopyOfBindingSource BindingSource;

            public object ValueProvider;
        }

        [DuckCopy]
        public struct CopyOfBindingSource
        {
            public string Id;
        }
    }
}
