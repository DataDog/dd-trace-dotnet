// <copyright file="DuckTypeAotAdditionalParityTests.Creation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// How proxies are created and what their members do: duck chaining of generic method parameters, a generic ToString of the
/// target, duck-chained values of reverse proxies, base constructors of class proxies, [DuckCopy] results, omitted optional
/// parameters, and the exception dynamic duck typing wraps when it can't create a proxy type.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Generic method parameters built on a type of another assembly than CoreLib (System.Collections): not duck chained.
    [InlineData("generic-list-parameter")]
    [InlineData("generic-dictionary-parameter")]
    // The target's public ToString() is a virtual generic method: calling it throws the runtime's BadImageFormatException.
    [InlineData("generic-to-string-override")]
    [InlineData("generic-to-string-override-class-proxy")]
    [InlineData("generic-to-string-new-slot")]
    // Null and non-null duck-chained values returned, written back and passed by a reverse proxy.
    [InlineData("reverse-duck-chained-values")]
    // A class proxy with a static constructor: its base constructor is the parameterless instance one.
    [InlineData("class-proxy-static-constructor")]
    // [DuckCopy] results through every entry point.
    [InlineData("duck-copy-entry-points")]
    // A proxy created over a null instance: calling a non-virtual member of the target throws a NullReferenceException.
    [InlineData("null-instance-non-virtual-member")]
    // Omitted optional parameters whose default is a native integer or the wrapper of an [IUnknownConstant].
    [InlineData("omitted-native-integer-default")]
    [InlineData("omitted-unknown-constant-default")]
    // Reflection.Emit can't create the proxy type: the DuckTypeException wraps its TypeLoadException.
    [InlineData("hidden-setter-type-load-failure")]
    public void GeneratedRegistryShouldCreateProxiesLikeDynamicMode(string scenario)
    {
        var (exercise, mapping) = scenario switch
        {
            "generic-list-parameter" => (
                () => DuckType.Create<ICreationListEchoProxy>(new CreationGenericEchoTarget())!.EchoList<int>([1, 2]),
                Mapping(typeof(ICreationListEchoProxy), typeof(CreationGenericEchoTarget))),
            "generic-dictionary-parameter" => (
                () => DuckType.Create<ICreationMapEchoProxy>(new CreationGenericEchoTarget())!.EchoMap<int>(new Dictionary<string, ICreationBox<int>> { ["a"] = new CreationBox<int>() }),
                Mapping(typeof(ICreationMapEchoProxy), typeof(CreationGenericEchoTarget))),
            "generic-to-string-override" => (
                (Func<object?>)(() => DescribeToString(DuckType.Create<ICreationNameProxy>(new CreationGenericToStringOverride())!)),
                Mapping(typeof(ICreationNameProxy), typeof(CreationGenericToStringOverride))),
            "generic-to-string-override-class-proxy" => (
                () => DescribeToString(DuckType.Create<ICreationNameClassProxy>(new CreationGenericToStringOverride())!),
                Mapping(typeof(ICreationNameClassProxy), typeof(CreationGenericToStringOverride))),
            "generic-to-string-new-slot" => (
                () => DescribeToString(DuckType.Create<ICreationNameProxy>(new CreationGenericToStringBase())!),
                Mapping(typeof(ICreationNameProxy), typeof(CreationGenericToStringBase))),
            "reverse-duck-chained-values" => (
                () => DescribeReverseChaining(new CreationReverseChainImpl()),
                Mapping(typeof(CreationReverseChainBase), typeof(CreationReverseChainImpl), reverse: true)),
            "class-proxy-static-constructor" => (
                () =>
                {
                    var calls = CreationStaticConstructorProxy.Calls;
                    return DuckType.Create<CreationStaticConstructorProxy>(new CreationNameTarget())!.Name + "|" + (CreationStaticConstructorProxy.Calls - calls);
                },
                Mapping(typeof(CreationStaticConstructorProxy), typeof(CreationNameTarget))),
            "duck-copy-entry-points" => (
                () => DescribeDuckCopy(new CreationCopyTarget()),
                Mapping(typeof(CreationCopy), typeof(CreationCopyTarget))),
            "null-instance-non-virtual-member" => (
                () => DuckType.GetOrCreateProxyType(typeof(ICreationNameProxy), typeof(CreationNameTarget)).CreateInstance<ICreationNameProxy>(null).Name,
                Mapping(typeof(ICreationNameProxy), typeof(CreationNameTarget))),
            "omitted-native-integer-default" => (
                () => DuckType.Create<ICreationEchoProxy>(new CreationNativeIntegerDefaultTarget())!.Echo(1),
                Mapping(typeof(ICreationEchoProxy), typeof(CreationNativeIntegerDefaultTarget))),
            "omitted-unknown-constant-default" => (
                () => DuckType.Create<ICreationEchoProxy>(new CreationUnknownConstantDefaultTarget())!.Echo(1),
                Mapping(typeof(ICreationEchoProxy), typeof(CreationUnknownConstantDefaultTarget))),
            _ => (
                () => DuckType.Create<ICreationHiddenSetterDerived>(new CreationNameTarget())!.Name,
                Mapping(typeof(ICreationHiddenSetterDerived), typeof(CreationNameTarget))),
        };

        // The duck-chained values of the reverse proxy are forward proxies.
        DuckTypeAotMapping[] mappings = scenario == "reverse-duck-chained-values"
                                            ? [mapping, Mapping(typeof(ICreationReverseValueProxy), typeof(CreationReverseValue))]
                                            : [mapping];
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureWithInnerException(exercise);
        WithGeneratedRegistry(() => CaptureWithInnerException(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"), mappings);
    }

    private static string DescribeToString(object proxy)
        => "duck=" + CaptureWithInnerException(() => ((IDuckType)proxy).ToString()) + "|object=" + CaptureWithInnerException(proxy.ToString) + "|interpolated=" + CaptureWithInnerException(() => $"{proxy}");

    private static string DescribeReverseChaining(CreationReverseChainImpl implementation)
    {
        var proxy = (CreationReverseChainBase)DuckType.CreateReverse(typeof(CreationReverseChainBase), implementation);
        return string.Join(
            ";",
            CaptureWithInnerException(() => proxy.GetValue() is null),
            CaptureWithInnerException(() => proxy.Value is null),
            CaptureWithInnerException(() => proxy.TryGet(out var value) + ":" + (value is null)),
            CaptureWithInnerException(() => RoundtripNull(proxy)),
            CaptureWithInnerException(() => proxy.Describe(null)),
            CaptureWithInnerException(() => proxy.Describe(new CreationReverseValue())),
            CaptureWithInnerException(() => SetAndRead(proxy, implementation, null)),
            CaptureWithInnerException(() => SetAndRead(proxy, implementation, new CreationReverseValue())));

        static string RoundtripNull(CreationReverseChainBase proxy)
        {
            CreationReverseValue? value = new CreationReverseValue();
            return proxy.Roundtrip(ref value) + ":" + (value is null);
        }

        static string SetAndRead(CreationReverseChainBase proxy, CreationReverseChainImpl implementation, CreationReverseValue? value)
        {
            proxy.Settable = value;
            return implementation.LastSet?.Name ?? "null";
        }
    }

    private static string DescribeDuckCopy(CreationCopyTarget target)
    {
        var result = DuckType.GetOrCreateProxyType(typeof(CreationCopy), typeof(CreationCopyTarget));
        var proxyType = result.ProxyType!;
        return string.Join(
            ";",
            CaptureWithInnerException(() => DuckType.Create<CreationCopy>(target).Name),
            CaptureWithInnerException(() => ((CreationCopy)DuckType.Create(typeof(CreationCopy), target)).Name),
            CaptureWithInnerException(() => ((CreationCopy)result.CreateInstance<object>(target)).Name),
            CaptureWithInnerException(() => result.CreateInstance<CreationCopy>(target).Name),
            CaptureWithInnerException(() => result.CreateInstance<ICreationNameProxy>(target).Name),
            CaptureWithInnerException(() => result.CreateInstance<CreationCopy>(null).Name),
            $"{result.Success},{result.CanCreate()},{proxyType.IsValueType},{proxyType == typeof(CreationCopy)},{typeof(IDuckType).IsAssignableFrom(proxyType)}",
            CaptureWithInnerException(() => proxyType.GetConstructors().Single().Invoke([target]) is IDuckType duckType ? duckType.Type.Name + "|" + ReferenceEquals(duckType.Instance, target) : "not IDuckType"));
    }

    public interface ICreationBox<T>
    {
    }

    public class CreationBox<T> : ICreationBox<T>
    {
    }

    public interface ICreationListEchoProxy
    {
        string EchoList<T>(List<T> items);
    }

    public interface ICreationMapEchoProxy
    {
        string EchoMap<T>(Dictionary<string, ICreationBox<T>> map);
    }

    public class CreationGenericEchoTarget
    {
        public string EchoList<T>(List<T> items) => "list:" + typeof(T).Name + ":" + items.Count;

        public string EchoMap<T>(Dictionary<string, ICreationBox<T>> map) => "map:" + typeof(T).Name + ":" + map.Count;
    }

    public interface ICreationNameProxy
    {
        string Name { get; }
    }

    [DuckAsClass]
    public interface ICreationNameClassProxy
    {
        string Name { get; }
    }

    public class CreationGenericToStringBase
    {
        public string Name => "name";

        public virtual string ToString<T>() => "generic-base";
    }

    public class CreationGenericToStringOverride : CreationGenericToStringBase
    {
        public override string ToString<T>() => "generic-override";
    }

    public abstract class CreationReverseChainBase
    {
        public abstract CreationReverseValue? Value { get; }

        public abstract CreationReverseValue? Settable { get; set; }

        public abstract CreationReverseValue? GetValue();

        public abstract bool TryGet(out CreationReverseValue? value);

        public abstract bool Roundtrip(ref CreationReverseValue? value);

        public abstract string Describe(CreationReverseValue? value);
    }

    public class CreationReverseValue
    {
        public string Name => "value";
    }

    public interface ICreationReverseValueProxy
    {
        string Name { get; }
    }

    public class CreationReverseChainImpl
    {
        public ICreationReverseValueProxy? LastSet { get; private set; }

        [DuckReverseMethod]
        public ICreationReverseValueProxy? Value => null;

        [DuckReverseMethod]
        public ICreationReverseValueProxy? Settable
        {
            get => LastSet;
            set => LastSet = value;
        }

        [DuckReverseMethod]
        public ICreationReverseValueProxy? GetValue() => null;

        [DuckReverseMethod]
        public bool TryGet(out ICreationReverseValueProxy? value)
        {
            value = null;
            return false;
        }

        [DuckReverseMethod]
        public bool Roundtrip(ref ICreationReverseValueProxy? value)
        {
            value = null;
            return true;
        }

        [DuckReverseMethod]
        public string Describe(ICreationReverseValueProxy? value) => value?.Name ?? "null";
    }

    public class CreationNameTarget
    {
        public string Name => "name";

        public string Value => "value";
    }

    public abstract class CreationStaticConstructorProxy
    {
        private static int _staticCalls;

        static CreationStaticConstructorProxy()
        {
            _staticCalls++;
        }

        protected CreationStaticConstructorProxy()
        {
            Calls++;
        }

        public static int Calls { get; private set; }

        public abstract string Name { get; }

        public override string ToString() => _staticCalls.ToString();
    }

    [DuckCopy]
    public struct CreationCopy
    {
        public string Name;
    }

    public class CreationCopyTarget
    {
        public string Name => "copy";
    }

    public interface ICreationEchoProxy
    {
        int Echo(int value);
    }

    public class CreationNativeIntegerDefaultTarget
    {
        public int Echo(int value, nint offset = 5) => value + (int)offset;
    }

    public class CreationUnknownConstantDefaultTarget
    {
#pragma warning disable CS0618 // UnknownWrapper is obsolete, but it's what the C# compiler passes for this parameter.
        public int Echo(int value, [Optional, IUnknownConstant] object wrapper) => value + (wrapper is UnknownWrapper ? 10 : 0);
#pragma warning restore CS0618
    }

    public interface ICreationHiddenSetterBase
    {
        string Name { get; set; }
    }

    public interface ICreationHiddenSetterDerived : ICreationHiddenSetterBase
    {
        new string Name { get; }
    }
}
