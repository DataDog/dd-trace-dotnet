// <copyright file="DuckTypeAotAdditionalParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

[Collection(nameof(DuckTypeAotProcessorConsoleCollection))]
public class DuckTypeAotAdditionalParityTests
{
    [Theory]
    [InlineData("method")]
    [InlineData("hint")]
    [InlineData("property")]
    [InlineData("field")]
    [InlineData("copy")]
    public void GeneratedRegistryShouldMatchInheritedGenericTargetMembers(string member)
    {
        var proxyType = member switch
        {
            "method" => typeof(IGenericEchoProxy),
            "hint" => typeof(IGenericHintEchoProxy),
            "property" => typeof(IGenericValueProxy),
            "field" => typeof(IGenericFieldProxy),
            _ => typeof(GenericCopy)
        };
        var expected = member switch
        {
            "method" or "hint" => "echo",
            "property" or "field" => "updated",
            _ => "initial|field"
        };
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be(expected);

        WithGeneratedRegistry(
            () => Exercise().Should().Be(expected),
            Mapping(proxyType, typeof(GenericLeafTarget)));

        string Exercise()
        {
            var target = new GenericLeafTarget { Value = "initial", Field = "field" };
            if (member == "method")
            {
                return DuckType.Create<IGenericEchoProxy>(target)!.Echo("echo");
            }

            if (member == "hint")
            {
                return DuckType.Create<IGenericHintEchoProxy>(target)!.Echo("echo");
            }

            if (member == "field")
            {
                var proxy = DuckType.Create<IGenericFieldProxy>(target)!;
                proxy.Field = "updated";
                return proxy.Field;
            }

            if (member == "property")
            {
                var proxy = DuckType.Create<IGenericValueProxy>(target)!;
                proxy.Value = "updated";
                return proxy.Value;
            }

            var copy = DuckType.Create<GenericCopy>(target);
            return copy.Value + "|" + copy.Field;
        }
    }

    [Fact]
    public void GeneratedRegistryShouldMatchInheritedGenericOptionalParameter()
    {
        WithGeneratedRegistry(
            () => Exercise().Should().Be(0),
            Mapping(typeof(IGenericOptionalProxy), typeof(GenericIntegerLeafTarget)));

        int Exercise()
            => DuckType.Create<IGenericOptionalProxy>(new GenericIntegerLeafTarget())!.DefaultValue(42);
    }

    [Fact]
    public void DynamicProxyShouldSupplyInheritedGenericOptionalParameter()
    {
        DuckType.ResetRuntimeModeForTests();
        DuckType.Create<IGenericOptionalProxy>(new GenericIntegerLeafTarget())!.DefaultValue(42).Should().Be(0);
    }

    [Fact]
    public void GeneratedRegistryShouldMatchOmittedOptionalConstants()
    {
        const string expected = "7|text|Monday|3.75|9|True|9223372036854775807|1.25|2.5|True|0";
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be(expected);
        WithGeneratedRegistry(
            () => Exercise().Should().Be(expected),
            Mapping(typeof(IOptionalConstantsProxy), typeof(OptionalConstantsTarget)));

        string Exercise() => DuckType.Create<IOptionalConstantsProxy>(new OptionalConstantsTarget())!.Format();
    }

    [Fact]
    public void GeneratedRegistryShouldCastReverseProxyThroughAncestorMapping()
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be("ancestor");
        WithGeneratedRegistry(
            () => Exercise().Should().Be("ancestor"),
            Mapping(typeof(ReverseDescendant), typeof(AnnotatedDelegation), reverse: true),
            Mapping(typeof(IGenericEchoProxy), typeof(ReverseAncestor)));

        string Exercise()
        {
            var reverse = DuckType.CreateReverse(typeof(ReverseDescendant), new AnnotatedDelegation());
            return DuckType.Create<IGenericEchoProxy>(reverse)!.Echo("ancestor");
        }
    }

    [Fact]
    public void GeneratedRegistryShouldRejectUnannotatedReverseImplementation()
    {
        DuckType.ResetRuntimeModeForTests();
        var dynamicFailure = Record.Exception(() => DuckType.CreateReverse(typeof(ReverseAncestor), new UnannotatedDelegation()));
        dynamicFailure.Should().NotBeNull();
        var expectedFailure = dynamicFailure!.GetBaseException().GetType();

        WithGeneratedRegistry(
            () =>
            {
                var actualFailure = Record.Exception(() => DuckType.CreateReverse(typeof(ReverseAncestor), new UnannotatedDelegation()));
                actualFailure.Should().NotBeNull("dynamic reverse proxies require an annotated implementation");
                actualFailure!.GetBaseException().GetType().Should().Be(expectedFailure);
            },
            Mapping(typeof(ReverseAncestor), typeof(UnannotatedDelegation), reverse: true));
    }

    [Fact]
    public void GeneratedRegistryShouldKeepUnannotatedConcreteReverseMethod()
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be("base:input");
        WithGeneratedRegistry(
            () => Exercise().Should().Be("base:input"),
            Mapping(typeof(ConcreteReverseBase), typeof(UnannotatedDelegation), reverse: true));

        string Exercise()
            => ((ConcreteReverseBase)DuckType.CreateReverse(typeof(ConcreteReverseBase), new UnannotatedDelegation())!).Echo("input");
    }

    [Fact]
    public void GeneratedRegistryShouldUseInheritedReverseMethodAttribute()
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be("override:input");
        WithGeneratedRegistry(
            () => Exercise().Should().Be("override:input"),
            Mapping(typeof(ReverseAncestor), typeof(OverrideDelegation), reverse: true));

        string Exercise()
            => ((ReverseAncestor)DuckType.CreateReverse(typeof(ReverseAncestor), new OverrideDelegation())!).Echo("input");
    }

    [Fact]
    public void GeneratedRegistryShouldRejectInheritedOnlyReverseImplementation()
    {
        DuckType.ResetRuntimeModeForTests();
        var dynamicFailure = Record.Exception(() => DuckType.CreateReverse(typeof(ReverseAncestor), new InheritedDelegation()));
        dynamicFailure.Should().NotBeNull();
        var expectedFailure = dynamicFailure!.GetBaseException().GetType();
        WithGeneratedRegistry(
            () =>
            {
                var actualFailure = Record.Exception(() => DuckType.CreateReverse(typeof(ReverseAncestor), new InheritedDelegation()));
                actualFailure.Should().NotBeNull();
                actualFailure!.GetBaseException().GetType().Should().Be(expectedFailure);
            },
            Mapping(typeof(ReverseAncestor), typeof(InheritedDelegation), reverse: true));
    }

    [Fact]
    public void GeneratedRegistryShouldNotReuseReverseAliasWithDifferentDeclaredMethods()
    {
        DuckType.ResetRuntimeModeForTests();
        DuckType.GetOrCreateReverseProxyType(typeof(ReverseAncestor), typeof(InheritedDelegation)).CanCreate().Should().BeFalse();
        WithGeneratedRegistry(
            () => DuckType.GetOrCreateReverseProxyType(typeof(ReverseAncestor), typeof(InheritedDelegation)).CanCreate().Should().BeFalse(),
            Mapping(typeof(ReverseAncestor), typeof(VirtualAnnotatedDelegation), reverse: true));
    }

    [Fact]
    public void GeneratedRegistryShouldMatchReverseChaining()
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be("inner|True");
        WithGeneratedRegistry(
            () => Exercise().Should().Be("inner|True"),
            Mapping(typeof(ITestDuckReverseChainProxy), typeof(TestDuckReverseChainTarget), reverse: true),
            Mapping(typeof(ITestDuckReverseChainInnerProxy), typeof(TestDuckReverseChainInnerTarget)));

        string Exercise()
        {
            var target = new TestDuckReverseChainTarget();
            var proxy = (ITestDuckReverseChainProxy)DuckType.CreateReverse(typeof(ITestDuckReverseChainProxy), target)!;
            var innerTarget = new TestDuckReverseChainInnerTarget("inner");
            proxy.Value = innerTarget;
            return proxy.Roundtrip(proxy.Value).Name + "|" + ReferenceEquals(((IDuckType)target.Value).Instance, innerTarget);
        }
    }

    [Fact]
    public void GeneratedRegistryShouldMatchReverseByRefConversions()
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be("from-out-roundtrip|22|42|99|100");
        WithGeneratedRegistry(
            () => Exercise().Should().Be("from-out-roundtrip|22|42|99|100"),
            Mapping(typeof(ITestDuckByRefReverseConversionProxy), typeof(TestDuckByRefReverseConversionTarget), reverse: true),
            Mapping(typeof(ITestDuckByRefReverseConversionInnerProxy), typeof(TestDuckByRefReverseConversionInnerTarget)));

        string Exercise()
        {
            var proxy = (ITestDuckByRefReverseConversionProxy)DuckType.CreateReverse(typeof(ITestDuckByRefReverseConversionProxy), new TestDuckByRefReverseConversionTarget())!;
            proxy.TryGetInner(out var inner).Should().BeTrue();
            proxy.RoundtripInner(ref inner).Should().BeTrue();
            int[] values = [21, 99, 0, 100];
            proxy.Increment(ref values[0]);
            proxy.GetNumber(out values[2]);
            return inner.Name + "|" + string.Join("|", values[0], values[2], values[1], values[3]);
        }
    }

    [Fact]
    public void GeneratedRegistryShouldRejectReverseConcreteParameterThatDynamicCannotSelect()
    {
        DuckType.ResetRuntimeModeForTests();
        var dynamicFailure = Record.Exception(() => DuckType.CreateReverse(typeof(IInvalidReverseContract), new InvalidReverseDelegation()));
        dynamicFailure.Should().NotBeNull();
        var expectedFailure = dynamicFailure!.GetBaseException().GetType();
        WithGeneratedRegistry(
            () =>
            {
                var actualFailure = Record.Exception(() => DuckType.CreateReverse(typeof(IInvalidReverseContract), new InvalidReverseDelegation()));
                actualFailure.Should().NotBeNull();
                actualFailure!.GetBaseException().GetType().Should().Be(expectedFailure);
            },
            Mapping(typeof(IInvalidReverseContract), typeof(InvalidReverseDelegation), reverse: true));
    }

    private static DuckTypeAotMapping Mapping(Type proxy, Type target, bool reverse = false)
        => new(
            proxy.FullName!,
            proxy.Assembly.GetName().Name!,
            target.FullName!,
            target.Assembly.GetName().Name!,
            reverse ? DuckTypeAotMappingMode.Reverse : DuckTypeAotMappingMode.Forward,
            DuckTypeAotMappingSource.MapFile);

    private static void WithGeneratedRegistry(Action assertions, params DuckTypeAotMapping[] mappings)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-additional-aot-parity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var loadContext = new AssemblyLoadContext("AdditionalParity", isCollectible: true);
        try
        {
            var mapPath = Path.Combine(directory, "map.json");
            var outputPath = Path.Combine(directory, "Registry.dll");
            File.WriteAllText(mapPath, JsonConvert.SerializeObject(new
            {
                mappings = mappings.Select(mapping => new
                {
                    mode = mapping.Mode.ToString().ToLowerInvariant(),
                    proxyType = mapping.ProxyTypeName,
                    proxyAssembly = mapping.ProxyAssemblyName,
                    targetType = mapping.TargetTypeName,
                    targetAssembly = mapping.TargetAssemblyName
                })
            }));
            var assemblyPath = typeof(DuckTypeAotAdditionalParityTests).Assembly.Location;
            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { assemblyPath },
                targetAssemblies: new[] { assemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Registry",
                trimmerDescriptorPath: outputPath + ".linker.xml",
                propsPath: outputPath + ".props");
            DuckTypeAotGenerateProcessor.Process(options).Should().Be(0);

            DuckType.ResetRuntimeModeForTests();
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(outputPath));
#else
            using var registryStream = File.OpenRead(outputPath);
            var registry = loadContext.LoadFromStream(registryStream);
#endif
            registry.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap")!
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            assertions();
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            loadContext.Unload();
            Directory.Delete(directory, recursive: true);
        }
    }

    public interface IGenericEchoProxy
    {
        string Echo(string value);
    }

    public interface IGenericValueProxy
    {
        string Value { get; set; }
    }

    public interface IGenericHintEchoProxy
    {
        [Duck(ParameterTypeNames = new[] { "System.String" })]
        string Echo(string value);
    }

    public interface IGenericFieldProxy
    {
        [DuckField]
        string Field { get; set; }
    }

    public interface IGenericOptionalProxy
    {
        int DefaultValue(int value);
    }

    public interface IOptionalConstantsProxy
    {
        string Format();
    }

    public class OptionalConstantsTarget
    {
        public string Format(
            [Optional, DefaultParameterValue(9)] object boxed,
            int number = 7,
            string text = "text",
            DayOfWeek day = DayOfWeek.Monday,
            decimal amount = 3.75m,
            bool flag = true,
            long large = long.MaxValue,
            float single = 1.25f,
            double precision = 2.5,
            int? nullable = null,
            DateTime date = default)
            => string.Join("|", number, text, day, amount.ToString(CultureInfo.InvariantCulture), boxed, flag, large, single.ToString(CultureInfo.InvariantCulture), precision.ToString(CultureInfo.InvariantCulture), nullable is null, date.Ticks);
    }

#pragma warning disable SA1401 // Public fields are part of the tested DuckCopy and target contracts.
    [DuckCopy]
    public struct GenericCopy
    {
        public string Value;

        [DuckField]
        public string Field;
    }

    public class GenericTargetBase<T>
    {
        public T Field = default!;

        public T Value { get; set; } = default!;

        public T Echo(T value) => value;

        public T DefaultValue(T value, T fallback = default!) => fallback;
    }
#pragma warning restore SA1401

    public class GenericLeafTarget : GenericTargetBase<string>
    {
    }

    public class GenericIntegerLeafTarget : GenericTargetBase<int>
    {
    }

    public abstract class ReverseAncestor
    {
        public abstract string Echo(string value);
    }

    public abstract class ReverseDescendant : ReverseAncestor
    {
    }

    public class AnnotatedDelegation
    {
        [DuckReverseMethod]
        public string Echo(string value) => value;
    }

    public class UnannotatedDelegation
    {
        public string Echo(string value) => value;
    }

    public class ConcreteReverseBase
    {
        public virtual string Echo(string value) => "base:" + value;
    }

    public class VirtualAnnotatedDelegation
    {
        [DuckReverseMethod]
        public virtual string Echo(string value) => "base:" + value;
    }

    public class OverrideDelegation : VirtualAnnotatedDelegation
    {
        public override string Echo(string value) => "override:" + value;
    }

    public class InheritedDelegation : VirtualAnnotatedDelegation
    {
    }

    public interface IInvalidReverseContract
    {
        string Echo(IGenericEchoProxy value);
    }

    public class InvalidReverseDelegation
    {
        [DuckReverseMethod]
        public string Echo(GenericLeafTarget value) => value.Value;
    }
}
