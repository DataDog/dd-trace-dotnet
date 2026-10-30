// <copyright file="DynamicDuckTypingChangesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    /// <summary>
    /// The intentional changes of dynamic duck typing listed in docs/development/DuckTyping.NativeAOT.md ("Changes to Dynamic Duck
    /// Typing") that the other tests don't cover.
    /// </summary>
    public class DynamicDuckTypingChangesTests
    {
        [Fact]
        public void ClassProxyWithAStaticConstructorShouldCallItsInstanceConstructor()
        {
            var calls = StaticConstructorProxy.Calls;

            var proxy = DuckType.Create<StaticConstructorProxy>(new NameTarget())!;

            proxy.Name.Should().Be("name");
            (StaticConstructorProxy.Calls - calls).Should().Be(1);
        }

        [Fact]
        public void OmittedNativeIntegerParameterShouldReceiveItsDefaultValue()
        {
            DuckType.Create<IEchoProxy>(new NativeIntegerDefaultTarget())!.Echo(1).Should().Be(6);
        }

        [Fact]
        public void OmittedNullableNativeIntegerParametersShouldReceiveTheirDefaultValues()
        {
            DuckType.Create<IEchoProxy>(new NullableNativeIntegerDefaultTarget())!.Echo(1).Should().Be(12);
        }

        [Fact]
        public void OmittedUnknownConstantParameterShouldReceiveAWrapperOfNull()
        {
            DuckType.Create<IEchoProxy>(new UnknownConstantDefaultTarget())!.Echo(1).Should().Be(11);
        }

        [Fact]
        public void GenericProxyMethodsShouldHaveTheConstraintsOfTheMethodTheyImplement()
        {
            // The target method has the same constraints: without them, calling it fails verification.
            DuckType.Create<IConstrainedFactoryProxy>(new ConstrainedFactoryTarget())!.Create<NameTarget>().Name.Should().Be("name");
            ((IConstrainedFactoryProxy)DuckType.CreateReverse(typeof(IConstrainedFactoryProxy), new ConstrainedFactoryDelegation())).Create<NameTarget>().Name.Should().Be("name");
            ((IStructDescriberProxy)DuckType.CreateReverse(typeof(IStructDescriberProxy), new StructDescriberDelegation())).Describe(4).Should().Be("struct:4");
        }

        [Fact]
        public void ProxyMethodsWithDefaultParameterValuesShouldBeCreated()
        {
            // The default value of each parameter is the one of that parameter (not of the previous one, nor of the return value).
            var proxy = DuckType.Create<IDefaultValuesProxy>(new DefaultValuesTarget())!;
            proxy.Format(3).Should().Be("3:x");
            var parameters = proxy.GetType().GetMethod(nameof(IDefaultValuesProxy.Format))!.GetParameters();
            parameters.Select(parameter => parameter.Name).Should().Equal("value", "text");
            parameters[1].DefaultValue.Should().Be("x");
        }

        [Fact]
        public void ProxyMethodsWithDefaultValuesTheRuntimeCantStoreShouldBeCreated()
        {
            // A null default of a value type (rejected by .NET Framework), a decimal, a native integer and a Missing default
            // can't be stored as parameter constants of the generated method: they're left out of its metadata.
            DuckType.Create<IUnstorableDefaultsProxy>(new UnstorableDefaultsTarget())!.Describe(1).Should().Be("1:False:1.5:5:missing");
        }

        [Fact]
        public void ObjectBasedCreationShouldNotUseDynamicInvoke()
        {
            // The typed activators are bound to object activators, which Create(Type, object), DuckAs, TryDuckCast and
            // CreateReverse(Type, object) call (before, dynamic duck typing used DynamicInvoke).
            var result = DuckType.GetOrCreateProxyType(typeof(INameProxy), typeof(NameTarget));
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            ((INameProxy)DuckType.Create(typeof(INameProxy), new NameTarget())).Name.Should().Be("name");
            var copyResult = DuckType.GetOrCreateProxyType(typeof(NameCopy), typeof(NameTarget));
            copyResult.UsesDynamicInvokeFallback.Should().BeFalse();
            ((NameCopy)DuckType.Create(typeof(NameCopy), new NameTarget())).Name.Should().Be("name");

            // Like DynamicInvoke, an exception of the activator is wrapped in a TargetInvocationException.
            var exception = Record.Exception(() => result.CreateInstance(new object()));
            exception.Should().BeOfType<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidCastException>();
        }

        [Fact]
        public void ReversePropertiesShouldHaveTheTypeOfThePropertyTheyImplement()
        {
            // The property of the delegation type is a proxy of the contract property's type: the reverse proxy's property has the
            // type of its getter (the contract's), so a forward duck cast over the reverse proxy duck chains it.
            var reverse = DuckType.CreateReverse(typeof(ChainContract), new ChainDelegation());
            reverse.GetType().GetProperty(nameof(ChainContract.Value))!.PropertyType.Should().Be(typeof(NameTarget));
            DuckType.Create<IChainView>(reverse).Value.Name.Should().Be("name");
        }

        [Fact]
        public void MethodFallbackNamesShouldBeTriedInOrderWhenArgumentsAreDuckChained()
        {
            // The argument is a proxy (duck chained): Type.GetMethod doesn't find the methods, the candidate scan does, for each
            // name; the first name that has one wins.
            var name = DuckType.Create<INameProxy>(new NameTarget());
            DuckType.Create<IFallbackDescribeProxy>(new FallbackDescribeTarget()).Describe(name).Should().Be("primary:name");
        }

        [Fact]
        public void MethodNamesShouldBeTrimmedButNotPropertyNamesWithoutFallbacks()
        {
            DuckType.Create<ITrimmedMethodNameProxy>(new NameTarget())!.Echo(7).Should().Be(7);

            Action createWithUntrimmedPropertyName = () => DuckType.Create<ITrimmedPropertyNameProxy>(new NameTarget());
            createWithUntrimmedPropertyName.Should().Throw<DuckTypePropertyOrFieldNotFoundException>();
        }

        [Fact]
        public void ForwardAndReverseProxiesOfTheSamePairShouldBeCachedSeparately()
        {
            // Before, one cache keyed by the pair served both directions: the second request got the proxy of the first one.
            var forward = DuckType.GetOrCreateProxyType(typeof(ISharedPairProxy), typeof(SharedPairTarget));
            var reverse = DuckType.GetOrCreateReverseProxyType(typeof(ISharedPairProxy), typeof(SharedPairTarget));

            forward.CanCreate().Should().BeTrue();
            reverse.CanCreate().Should().BeTrue();
            reverse.ProxyType.Should().NotBe(forward.ProxyType);
            DuckType.Create<ISharedPairProxy>(new SharedPairTarget())!.Name.Should().Be("shared");
            ((ISharedPairProxy)DuckType.CreateReverse(typeof(ISharedPairProxy), new SharedPairTarget())).Name.Should().Be("shared");
        }

        [Fact]
        public void ReverseProxyOfAForwardProxyShouldBeItsOriginalInstance()
        {
            // The original instance already is of the requested type. Before, a reverse proxy over the generated proxy type
            // was attempted, which normally failed.
            var original = new RoundTripTarget();
            var forward = DuckType.Create<IRoundTripProxy>(original)!;

            DuckType.CreateReverse(typeof(IRoundTripContract), forward).Should().BeSameAs(original);
            forward.DuckImplement(typeof(IRoundTripContract)).Should().BeSameAs(original);
            forward.TryDuckImplement(typeof(IRoundTripContract), out var value).Should().BeTrue();
            value.Should().BeSameAs(original);
        }

        [Fact]
        public void ExplicitInterfaceTypeNameShouldApplyToProperties()
        {
            // The target only implements the property explicitly. Before, ExplicitInterfaceTypeName only applied to methods.
            DuckType.Create<IExplicitPropertyProxy>(new ExplicitPropertyTarget())!.Value.Should().Be("explicit");
        }

        [Fact]
        public void OmittedParameterWithASmallNegativeDefaultShouldReceiveIt()
        {
            // The default is loaded with ldc.i4.s, whose operand is a signed byte.
            DuckType.Create<IEchoProxy>(new NegativeDefaultTarget())!.Echo(1).Should().Be(-1);
        }

        [Fact]
        public void ValueWithTypeParameterShouldPassItsValueToAParameterOfTheValueType()
        {
            DuckType.Create<IValueWithTypeProxy>(new UnwrappedValueTarget())!.Get(ValueWithType<int>.Create(1, typeof(int))).Should().Be(2);
        }

        [Fact]
        public void ValueWithTypeParameterShouldBePassedAsItselfToAParameterOfItsOwnType()
        {
            // As before ValueWithType<T> parameters were unwrapped: in forward and reverse proxies.
            DuckType.Create<IValueWithTypeProxy>(new WrappedValueTarget())!.Get(ValueWithType<int>.Create(1, typeof(int))).Should().Be(3);
            ((IValueWithTypeProxy)new WrappedValueDelegation().DuckImplement(typeof(IValueWithTypeProxy))).Get(ValueWithType<int>.Create(1, typeof(int))).Should().Be(4);
        }

        public interface IValueWithTypeProxy
        {
            int Get(ValueWithType<int> value);
        }

        public class UnwrappedValueTarget
        {
            public int Get(int value) => value + 1;
        }

        public class WrappedValueTarget
        {
            public int Get(ValueWithType<int> value) => value.Value + 2;
        }

        public class WrappedValueDelegation
        {
            [DuckReverseMethod]
            public int Get(ValueWithType<int> value) => value.Value + 3;
        }

        public interface IEchoProxy
        {
            int Echo(int value);
        }

        public interface INameProxy
        {
            string Name { get; }
        }

        public interface IFallbackDescribeProxy
        {
            [Duck(Name = "Primary,Secondary")]
            string Describe(INameProxy value);
        }

        public class FallbackDescribeTarget
        {
            public string Secondary(NameTarget value) => "secondary:" + value.Name;

            public string Primary(NameTarget value) => "primary:" + value.Name;
        }

        public interface IChainView
        {
            INameProxy Value { get; }
        }

        public abstract class ChainContract
        {
            public abstract NameTarget Value { get; }
        }

        public class ChainDelegation
        {
            [DuckReverseMethod]
            public INameProxy Value => DuckType.Create<INameProxy>(new NameTarget());
        }

        public interface ITrimmedMethodNameProxy
        {
            [Duck(Name = " Echo ")]
            int Echo(int value);
        }

        public interface ITrimmedPropertyNameProxy
        {
            [Duck(Name = " Name ")]
            string Name { get; }
        }

        public class NameTarget
        {
            public string Name => "name";

            public int Echo(int value) => value;
        }

        public abstract class StaticConstructorProxy
        {
            private static int _staticCalls;

            static StaticConstructorProxy()
            {
                _staticCalls++;
            }

            protected StaticConstructorProxy()
            {
                Calls++;
            }

            public static int Calls { get; private set; }

            public abstract string Name { get; }

            public override string ToString() => _staticCalls.ToString();
        }

        public interface IConstrainedFactoryProxy
        {
            T Create<T>()
                where T : class, new();
        }

        public interface IStructDescriberProxy
        {
            string Describe<T>(T value)
                where T : struct;
        }

        [DuckCopy]
        public struct NameCopy
        {
            public string Name;
        }

        public interface IUnstorableDefaultsProxy
        {
            string Describe(int value, [Optional] object missing, CancellationToken token = default, decimal amount = 1.5m, nint offset = 5);
        }

        public class UnstorableDefaultsTarget
        {
            public string Describe(int value, object missing, CancellationToken token, decimal amount, nint offset)
                => value + ":" + token.CanBeCanceled + ":" + amount.ToString(CultureInfo.InvariantCulture) + ":" + offset + ":" + (missing == Type.Missing ? "missing" : "other");
        }

        public interface IDefaultValuesProxy
        {
            string Format(int value, string text = "x");
        }

        public class ConstrainedFactoryTarget
        {
            public T Create<T>()
                where T : class, new()
                => new T();
        }

        public class ConstrainedFactoryDelegation
        {
            [DuckReverseMethod]
            public T Create<T>()
                where T : class, new()
                => new T();
        }

        public class StructDescriberDelegation
        {
            [DuckReverseMethod]
            public string Describe<T>(T value)
                where T : struct
                => "struct:" + value;
        }

        public class DefaultValuesTarget
        {
            public string Format(int value, string text) => value + ":" + text;
        }

        public class NativeIntegerDefaultTarget
        {
            public int Echo(int value, nint offset = 5) => value + (int)offset;
        }

        public class NullableNativeIntegerDefaultTarget
        {
            public int Echo(int value, nint? offset = 5, nuint? extra = 6) => value + (int)offset!.Value + (int)extra!.Value;
        }

        public class UnknownConstantDefaultTarget
        {
#pragma warning disable CS0618 // UnknownWrapper is obsolete on .NET 6+, but it's what the C# compiler passes for this parameter.
            public int Echo(int value, [Optional, IUnknownConstant] object wrapper) => value + (wrapper is UnknownWrapper ? 10 : 0);
#pragma warning restore CS0618
        }

        public interface ISharedPairProxy
        {
            string Name { get; }
        }

        public class SharedPairTarget
        {
            [DuckReverseMethod]
            public string Name => "shared";
        }

        public interface IRoundTripContract
        {
            string Name { get; }
        }

        public interface IRoundTripProxy
        {
            string Name { get; }
        }

        public class RoundTripTarget : IRoundTripContract
        {
            public string Name => "original";
        }

        public interface IExplicitValue
        {
            string Value { get; }
        }

        public interface IExplicitPropertyProxy
        {
            [Duck(ExplicitInterfaceTypeName = "*")]
            string Value { get; }
        }

        public class ExplicitPropertyTarget : IExplicitValue
        {
            string IExplicitValue.Value => "explicit";
        }

        public class NegativeDefaultTarget
        {
            public int Echo(int value, int offset = -2) => value + offset;
        }
    }
}
