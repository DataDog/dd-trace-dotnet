// <copyright file="DuckTypeAotAdditionalParityTests.Registrations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Scenarios where the registrations of the AOT registry (duck chaining, closed generics, arrays, derived targets...)
/// have to behave like the proxies dynamic duck typing creates.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // A CoreLib interface argument assignable to the parameter is cast, not duck chained.
    [InlineData("assignable-corelib-argument", "scope:2")]
    // An interface argument passed to an object parameter is passed as is.
    [InlineData("interface-to-object-argument", "registered:scope:0")]
    // A CoreLib interface return type is a cast, which fails for a value that doesn't implement it.
    [InlineData("corelib-return-cast", "throws:InvalidCastException")]
    public void GeneratedRegistryShouldDuckChainLikeDynamicMode(string scenario, string expected)
    {
        Func<object?> exercise = scenario switch
        {
            "assignable-corelib-argument" => () => DuckType.Create<IScopeContextProxy>(new ScopeContextTarget())!
                                                          .PushProperties(new List<KeyValuePair<string, object>> { new("a", 1), new("b", 2) }),
            "interface-to-object-argument" => () => DuckType.Create<IScopeContextProxy>(new ScopeContextTarget())!.Register(new CountScope(0)),
            _ => () => DuckType.Create<IScopeContextProxy>(new ScopeContextTarget())!.Items,
        };

        AssertSameOutcome(expected, exercise, Mapping(typeof(IScopeContextProxy), typeof(ScopeContextTarget)));
    }

    [Theory]
    // A target that implements the closed proxy contract still gets a proxy, which honors [Duck] renames.
    [InlineData("implemented-contract", "True|2|False")]
    // A failing closed generic mapping replays the dynamic failure, not a missing registration.
    [InlineData("failing-mapping", "throws:DuckTypeTargetMethodNotFoundException")]
    public void GeneratedRegistryShouldCreateClosedGenericProxiesLikeDynamicMode(string scenario, string expected)
    {
        var (proxyType, exercise) = scenario switch
        {
            "implemented-contract" => (typeof(IGenericValue<int>), (Func<object?>)(() =>
            {
                var proxy = DuckType.Create(typeof(IGenericValue<int>), new GenericValueTarget<int>(1, 2))!;
                var proxyType = DuckType.GetOrCreateProxyType(typeof(IGenericValue<int>), typeof(GenericValueTarget<int>)).ProxyType!;
                return FormattableString.Invariant($"{proxy is IDuckType}|{((IGenericValue<int>)proxy).Value}|{proxyType.IsInterface}");
            })),
            _ => (typeof(IGenericValueWithMissingMethod<int>), () => DuckType.Create<IGenericValueWithMissingMethod<int>>(new GenericValueTarget<int>(1, 2))),
        };

        AssertSameOutcome(expected, exercise, Mapping(proxyType, typeof(GenericValueTarget<int>)));
    }

    [Fact]
    public void GeneratedRegistryShouldSupportArrayTargets()
    {
        DuckType.ResetRuntimeModeForTests();
        Func<object?> exercise = () =>
        {
            var proxy = DuckType.Create<ILengthProxy>(new[] { 1, 2, 3 })!;
            return FormattableString.Invariant($"{proxy.Length}|{((IDuckType)proxy).Type}");
        };
        CaptureOutcome(exercise).Should().Be("3|System.Int32[]");

        WithGeneratedRegistry(
            () => CaptureOutcome(exercise).Should().Be("3|System.Int32[]"),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(ILengthProxy), typeof(int[])));
    }

    [Theory]
    // A derived target gets its own proxy: IDuckType.Type and ProxyType describe the derived type.
    [InlineData("derived-identity", "derived|DerivedNamedTarget|False")]
    // An override of a `new virtual` method is a different slot than the mapped base method.
    [InlineData("new-virtual-chain", "derived|mid|base")]
    // When dynamic duck typing can't create the proxy of the mapped type, nor of its derived types, neither can the registry.
    [InlineData("failing-base", "False|throws:DuckTypeTargetPropertyAmbiguousMatchException")]
    public void GeneratedRegistryShouldCreateDerivedTargetProxiesLikeDynamicMode(string scenario, string expected)
    {
        var (mapping, exercise) = scenario switch
        {
            "derived-identity" => (Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)), (Func<object?>)(() =>
            {
                var proxy = DuckType.Create<INamedProxy>(new DerivedNamedTarget())!;
                var proxyType = DuckType.GetOrCreateProxyType(typeof(INamedProxy), typeof(DerivedNamedTarget)).ProxyType!;
                return FormattableString.Invariant($"{proxy.Name}|{((IDuckType)proxy).Type.Name}|{proxyType.IsInterface}");
            })),
            "new-virtual-chain" => (Mapping(typeof(IFooProxy), typeof(FooBase)), () =>
                FormattableString.Invariant($"{DuckType.Create<IFooProxy>(new FooDerived())!.Foo()}|{DuckType.Create<IFooProxy>(new FooMid())!.Foo()}|{DuckType.Create<IFooProxy>(new FooBase())!.Foo()}")),
            _ => (Mapping(typeof(NameCopy), typeof(NameBase)), () =>
                FormattableString.Invariant($"{DuckType.CanCreate<NameCopy>(new NameDerived())}|{CaptureOutcome(() => DuckType.Create<NameCopy>(new NameDerived()).Name)}")),
        };

        AssertSameOutcome(expected, exercise, mapping);
    }

    [Fact]
    public void GeneratedRegistryShouldCreateForwardProxiesOfGeneratedReverseProxiesLikeDynamicMode()
    {
        // Dynamic duck typing creates the forward proxy of the runtime type of the instance (the reverse proxy type): its
        // IDuckType.Type is that type, and its ProxyType a generated type.
        AssertSameOutcome(
            "e|runtime-type|proxy-type-is-interface:False",
            () =>
            {
                var reverse = DuckType.CreateReverse(typeof(ReverseDescendant), new AnnotatedDelegation());
                var forward = DuckType.Create<IGenericEchoProxy>(reverse)!;
                var type = ((IDuckType)forward).Type == reverse.GetType() ? "runtime-type" : ((IDuckType)forward).Type.Name;
                var proxyType = DuckType.GetOrCreateProxyType(typeof(IGenericEchoProxy), reverse.GetType()).ProxyType!;
                return FormattableString.Invariant($"{forward.Echo("e")}|{type}|proxy-type-is-interface:{proxyType.IsInterface}");
            },
            Mapping(typeof(ReverseDescendant), typeof(AnnotatedDelegation), reverse: true),
            Mapping(typeof(IGenericEchoProxy), typeof(ReverseAncestor)));
    }

    [Theory]
    // The fields of a closed generic [DuckCopy] struct have the types of its generic arguments.
    [InlineData("non-generic-field", "base")]
    [InlineData("generic-parameter-field", "42")]
    [InlineData("generic-list-field", "a,b")]
    [InlineData("generic-array-field", "1,2")]
    public void GeneratedRegistryShouldCopyIntoClosedGenericDuckCopyStructs(string scenario, string expected)
    {
        var (mapping, exercise) = scenario switch
        {
            "non-generic-field" => (Mapping(typeof(GenericNameCopy<int>), typeof(BaseNamedTarget)), (Func<object?>)(() => DuckType.Create<GenericNameCopy<int>>(new BaseNamedTarget()).Name)),
            "generic-parameter-field" => (Mapping(typeof(GenericValueCopy<int>), typeof(IntValueTarget)), () => DuckType.Create<GenericValueCopy<int>>(new IntValueTarget()).Value),
            "generic-list-field" => (Mapping(typeof(GenericListCopy<string>), typeof(StringListTarget)), () => string.Join(",", DuckType.Create<GenericListCopy<string>>(new StringListTarget()).Items)),
            _ => (Mapping(typeof(GenericArrayCopy<int>), typeof(IntArrayTarget)), () => string.Join(",", DuckType.Create<GenericArrayCopy<int>>(new IntArrayTarget()).Values)),
        };

        AssertSameOutcome(expected, exercise, mapping);
    }

    [Theory]
    // A type built on the generic parameter of a generic method (here IValueBox<T>) is duck chained like any other type: the
    // return value, the out parameter, and the argument (whose instance is passed to the target).
    [InlineData("return", "chained:5")]
    [InlineData("out-parameter", "chained:5")]
    [InlineData("parameter", "5")]
    public void GeneratedRegistryShouldDuckChainTheTypesOfGenericMethodsLikeDynamicMode(string scenario, string expected)
    {
        Func<object?> exercise = scenario switch
        {
            "return" => () => DescribeBox(DuckType.Create<IBoxWrapperProxy>(new BoxWrapper())!.Wrap(5)),
            "out-parameter" => () =>
            {
                DuckType.Create<IBoxWrapperProxy>(new BoxWrapper())!.TryWrap(5, out var box);
                return DescribeBox(box);
            },
            _ => () => DuckType.Create<IBoxWrapperProxy>(new BoxWrapper())!.Unwrap(DuckType.Create<IValueBox<int>>(new PlainValueBox<int>(5))!),
        };

        AssertSameOutcome(expected, exercise, Mapping(typeof(IBoxWrapperProxy), typeof(BoxWrapper)), Mapping(typeof(IValueBox<int>), typeof(PlainValueBox<int>)));

        static string DescribeBox(IValueBox<int> box) => (box is IDuckType ? "chained" : "cast") + ":" + box.Value.ToString(CultureInfo.InvariantCulture);
    }

    [Theory]
    // An array of a derived type is an array of the mapped element type (array covariance): dynamic duck typing creates its
    // proxy, and so does the registry. The derived element type can be abstract or an interface, and an array of arrays of a
    // derived type is an array of arrays of the mapped element type.
    [InlineData("derived-class", "3|ArrayDerivedElement[]")]
    [InlineData("abstract-derived-class", "3|ArrayAbstractDerivedElement[]")]
    [InlineData("derived-interface", "3|IArrayDerivedElement[]")]
    [InlineData("array-of-arrays", "3|ArrayDerivedElement[][]")]
    public void GeneratedRegistryShouldSupportCovariantArrayTargets(string scenario, string expected)
    {
        var (array, mappedType) = scenario switch
        {
            "derived-class" => ((Array)new[] { new ArrayDerivedElement(), new ArrayDerivedElement(), new ArrayDerivedElement() }, typeof(ArrayBaseElement[])),
            "abstract-derived-class" => (new ArrayAbstractDerivedElement[3], typeof(ArrayBaseElement[])),
            "derived-interface" => (new IArrayDerivedElement[3], typeof(IArrayBaseElement[])),
            _ => (new ArrayDerivedElement[3][], typeof(ArrayBaseElement[][])),
        };

        AssertSameOutcome(
            expected,
            () =>
            {
                var proxy = DuckType.Create<ILengthProxy>(array)!;
                return FormattableString.Invariant($"{proxy.Length}|{((IDuckType)proxy).Type.Name}");
            },
            Mapping(typeof(ILengthProxy), mappedType));
    }

    [Fact]
    public void GeneratedRegistryShouldReportArrayMembersOnlyTheRuntimeDefines()
    {
        // Known limitation: the Get, Set and Address methods of an array type only exist at runtime, so the registry can't bind
        // them, where dynamic duck typing can. The mapping isn't compatible (and the registry doesn't claim to replay a
        // dynamic failure), so verify-compat reports it.
        DuckType.ResetRuntimeModeForTests();
        DuckType.Create<IArrayGetProxy>(new[] { 4, 5, 6 })!.At(1).Should().Be(5);
        WithGeneratedRegistry(
            () => { },
            matrix =>
            {
                var entry = matrix.Mappings.Should().ContainSingle().Which;
                entry.Status.Should().Be(DuckTypeAotCompatibilityStatuses.MissingTargetMethod);
                entry.DynamicFailureReplayed.Should().BeFalse();
                entry.Details.Should().Contain("binds the method 'Int32 Get(Int32)' the runtime adds to array type 'System.Int32[]'");
            },
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(IArrayGetProxy), typeof(int[])));
    }

    [Fact]
    public void GeneratedRegistryShouldRegisterEverySpellingOfAMappingOnce()
    {
        // Type.FullName (what the discovery recorder writes) qualifies generic arguments with Version, Culture and
        // PublicKeyToken; generic instantiation roots and hand-written maps often don't. All of them are the same mapping:
        // registering one proxy each would make the registry initialization fail with a registration conflict.
        var proxyDefinitionName = typeof(IBoxProxy<>).FullName;
        var targetDefinitionName = typeof(Box<>).FullName;
        var assemblyName = typeof(DuckTypeAotAdditionalParityTests).Assembly.GetName().Name!;
        var spellings = new[]
        {
            (typeof(IBoxProxy<string>).FullName!, typeof(Box<string>).FullName!),
            ($"{proxyDefinitionName}[[System.String, System.Private.CoreLib]]", $"{targetDefinitionName}[[System.String, System.Private.CoreLib]]"),
            ($"{proxyDefinitionName}[[System.String]]", $"{targetDefinitionName}[[System.String]]"),
            // Unqualified generic arguments (like Type.GetType accepts them) are closed types too.
            ($"{proxyDefinitionName}[System.String]", $"{targetDefinitionName}[System.String]"),
        };

        DuckType.ResetRuntimeModeForTests();
        Func<object?> exercise = () => DuckType.Create<IBoxProxy<string>>(new Box<string>("boxed"))!.Value;
        CaptureOutcome(exercise).Should().Be("boxed");

        var directory = CreateTemporaryDirectory();
        var loadContext = new AssemblyLoadContext("AdditionalParity-Spellings", isCollectible: true);
        try
        {
            var mapPath = Path.Combine(directory, "map.json");
            var entries = new List<object>();
            foreach (var (proxyTypeName, targetTypeName) in spellings)
            {
                entries.Add(new { mode = "forward", proxyType = proxyTypeName, proxyAssembly = assemblyName, targetType = targetTypeName, targetAssembly = assemblyName });
            }

            File.WriteAllText(mapPath, JsonConvert.SerializeObject(new { mappings = entries }));
            var options = CreateGenerateOptions(directory, mapPath, targetFolders: []);
            DuckTypeAotGenerateProcessor.Process(options).Should().Be(0);

            DuckType.ResetRuntimeModeForTests();
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(options.OutputPath));
#else
            using var registryStream = File.OpenRead(options.OutputPath);
            var registry = loadContext.LoadFromStream(registryStream);
#endif
            registry.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap")!
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            CaptureOutcome(exercise).Should().Be("boxed");
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            loadContext.Unload();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    // A Nullable<T> value chained from a member creates the proxy of Nullable<T>, as mapped.
    [InlineData("nullable", "True")]
    // The underlying type gets its own proxy: it doesn't have the members of Nullable<T>.
    [InlineData("underlying", "throws:DuckTypePropertyOrFieldNotFoundException")]
    public void GeneratedRegistryShouldKeepNullableAndUnderlyingTypeProxiesApart(string scenario, string expected)
    {
        Func<object?> exercise = scenario == "nullable"
                                     ? () => DuckType.CreateCache<INullableShapeProxy>.CreateFrom<NullableShapeValue?>(new NullableShapeValue(1))!.HasValue
                                     : () => DuckType.Create<INullableShapeProxy>(new NullableShapeValue(1));

        DuckType.ResetRuntimeModeForTests();
        CaptureOutcome(exercise).Should().Be(expected);
        WithGeneratedRegistry(
            () => CaptureOutcome(exercise).Should().Be(expected),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(INullableShapeProxy), typeof(NullableShapeValue?)));
    }

    public interface IScopeContextProxy
    {
        IDisposable PushProperties(IReadOnlyList<KeyValuePair<string, object>> properties);

        string Register(IDisposable value);

        IEnumerable Items { get; }
    }

    public class ScopeContextTarget
    {
        public object Items => 42;

        public IDisposable PushProperties(IReadOnlyCollection<KeyValuePair<string, object>> properties) => new CountScope(properties.Count);

        public string Register(object value) => "registered:" + value;
    }

    public sealed class CountScope : IDisposable
    {
        private readonly int _count;

        public CountScope(int count) => _count = count;

        public void Dispose()
        {
        }

        public override string ToString() => FormattableString.Invariant($"scope:{_count}");
    }

    public interface IGenericValue<T>
    {
        [Duck(Name = "Other")]
        T Value { get; }
    }

    public interface IGenericValueWithMissingMethod<T>
    {
        T Value { get; }

        string Missing();
    }

    public class GenericValueTarget<T> : IGenericValue<T>
    {
        public GenericValueTarget(T value, T other)
        {
            Value = value;
            Other = other;
        }

        public T Value { get; }

        public T Other { get; }
    }

    public interface ILengthProxy
    {
        int Length { get; }
    }

    public interface INamedProxy
    {
        string Name { get; }
    }

    public class BaseNamedTarget
    {
        public virtual string Name => "base";
    }

    public class DerivedNamedTarget : BaseNamedTarget
    {
        public override string Name => "derived";
    }

    public interface IFooProxy
    {
        string Foo();
    }

    public class FooBase
    {
        public virtual string Foo() => "base";
    }

    public class FooMid : FooBase
    {
        public new virtual string Foo() => "mid";
    }

    public class FooDerived : FooMid
    {
        public override string Foo() => "derived";
    }

    [DuckCopy]
    public struct NameCopy
    {
        public int Name;
    }

    public class NameRoot
    {
        public string Name => "root";
    }

    public class NameBase : NameRoot
    {
        public new int Name => 1;
    }

    public class NameDerived : NameBase
    {
    }

    [DuckCopy]
    public struct GenericNameCopy<T>
    {
        public string Name;
    }

    public class ArrayBaseElement
    {
    }

    public class ArrayDerivedElement : ArrayBaseElement
    {
    }

    public abstract class ArrayAbstractDerivedElement : ArrayBaseElement
    {
    }

    public interface IArrayBaseElement
    {
    }

    public interface IArrayDerivedElement : IArrayBaseElement
    {
    }

    [DuckCopy]
    public struct GenericValueCopy<T>
    {
        public T Value;
    }

    [DuckCopy]
    public struct GenericListCopy<T>
    {
        public List<T> Items;
    }

    [DuckCopy]
    public struct GenericArrayCopy<T>
    {
        public T[] Values;
    }

    public class IntValueTarget
    {
        public int Value => 42;
    }

    public class StringListTarget
    {
        public List<string> Items => new() { "a", "b" };
    }

    public class IntArrayTarget
    {
        public int[] Values => new[] { 1, 2 };
    }

    public interface IValueBox<T>
    {
        T Value { get; }
    }

    public class PlainValueBox<T>
    {
        public PlainValueBox(T value) => Value = value;

        public T Value { get; }
    }

    public interface IBoxWrapperProxy
    {
        IValueBox<T> Wrap<T>(T value);

        void TryWrap<T>(T value, out IValueBox<T> box);

        T Unwrap<T>(IValueBox<T> box);
    }

    public class BoxWrapper
    {
        public PlainValueBox<T> Wrap<T>(T value) => new(value);

        public void TryWrap<T>(T value, out PlainValueBox<T> box) => box = new PlainValueBox<T>(value);

        public T Unwrap<T>(PlainValueBox<T> box) => box.Value;
    }

    public interface IArrayGetProxy
    {
        [Duck(Name = "Get")]
        int At(int index);
    }

    public interface IBoxProxy<T>
    {
        T Value { get; }
    }

    public class Box<T>
    {
        public Box(T value) => Value = value;

        public T Value { get; }
    }

    public interface INullableShapeProxy
    {
        bool HasValue { get; }
    }

    public readonly struct NullableShapeValue
    {
        public NullableShapeValue(int value) => Value = value;

        public int Value { get; }
    }
}
