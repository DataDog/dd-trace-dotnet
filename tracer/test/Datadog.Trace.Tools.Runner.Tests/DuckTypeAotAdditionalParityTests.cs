// <copyright file="DuckTypeAotAdditionalParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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

    [Theory]
    [InlineData(typeof(OptionalConstantsTarget), "7|text|Monday|3.75|9|True|9223372036854775807|1.25|2.5|True|0")]
    [InlineData(typeof(NullableOptionalConstantsTarget), "5|Friday|3|True")]
    [InlineData(typeof(NegativeOptionalConstantsTarget), "-5|-3|-100|4294967291|-2|-2|-1.5|-7|-9|200")]
    [InlineData(typeof(VirtualInOptionalConstantsTarget), "3|text|00000000-0000-0000-0000-000000000000")]
    [InlineData(typeof(MissingOptionalObjectTarget), "Type.Missing|0|null")]
    public void GeneratedRegistryShouldMatchOmittedOptionalConstants(Type targetType, string expected)
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be(expected);
        WithGeneratedRegistry(
            () => Exercise().Should().Be(expected),
            Mapping(typeof(IOptionalConstantsProxy), targetType));

        string Exercise() => DuckType.Create<IOptionalConstantsProxy>(Activator.CreateInstance(targetType)!)!.Format();
    }

    [Fact]
    public void GeneratedRegistryShouldMatchOmittedGenericInOptionalParameter()
    {
        const string expected = "0|null|0,0,null";
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be(expected);
        WithGeneratedRegistry(
            () => Exercise().Should().Be(expected),
            Mapping(typeof(IGenericInOptionalProxy), typeof(GenericInOptionalTarget)));

        string Exercise()
        {
            var proxy = DuckType.Create<IGenericInOptionalProxy>(new GenericInOptionalTarget())!;
            return string.Join("|", proxy.Describe<int>(), proxy.Describe<string>(), proxy.Describe<MultiFieldStruct>());
        }
    }

    [Fact]
    public void GeneratedRegistryShouldTreatExplicitInterfaceTypeNameAsSingleName()
    {
        // Dynamic duck typing doesn't split ExplicitInterfaceTypeName on commas, so neither mode can bind the explicit
        // implementation through a comma separated list. The generator has to reject it itself: if it bound a split name,
        // only the dynamic validation would reject the mapping, with a different status.
        DuckType.ResetRuntimeModeForTests();
        DuckType.GetOrCreateProxyType(typeof(ICommaSeparatedExplicitInterfaceProxy), typeof(ExplicitInterfaceTarget)).CanCreate().Should().BeFalse();
        WithGeneratedRegistry(
            () => DuckType.GetOrCreateProxyType(typeof(ICommaSeparatedExplicitInterfaceProxy), typeof(ExplicitInterfaceTarget)).CanCreate().Should().BeFalse(),
            matrix => matrix.Mappings.Should().ContainSingle().Which.Status.Should().Be(DuckTypeAotCompatibilityStatuses.MissingTargetMethod),
            Mapping(typeof(ICommaSeparatedExplicitInterfaceProxy), typeof(ExplicitInterfaceTarget)));
    }

    [Theory]
    // Type.GetMethod("Echo", [object]) doesn't match Echo(string), so the scan only accepts the explicit implementation.
    [InlineData(typeof(IExplicitEchoObjectProxy), "explicit:value")]
    // Type.GetMethod("Echo", [string]) finds the public method before ExplicitInterfaceTypeName is considered.
    [InlineData(typeof(IExplicitEchoStringProxy), "public:value")]
    public void GeneratedRegistryShouldMatchExplicitInterfaceTypeNameResolution(Type proxyType, string expected)
    {
        DuckType.ResetRuntimeModeForTests();
        Exercise().Should().Be(expected);
        WithGeneratedRegistry(
            () => Exercise().Should().Be(expected),
            Mapping(proxyType, typeof(ExplicitAndPublicEchoTarget)));

        string Exercise()
            => proxyType == typeof(IExplicitEchoObjectProxy)
                   ? DuckType.Create<IExplicitEchoObjectProxy>(new ExplicitAndPublicEchoTarget())!.Echo("value")
                   : DuckType.Create<IExplicitEchoStringProxy>(new ExplicitAndPublicEchoTarget())!.Echo("value");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MappingResolverShouldSkipInvalidAssembliesInTargetFolders(bool truncatedManagedAssembly)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            // Target folders (e.g. RID-specific outputs) contain files matched by the *.dll filter that aren't managed assemblies.
            var targetFolder = Path.Combine(directory, "target");
            Directory.CreateDirectory(targetFolder);
            var invalidAssemblyPath = Path.Combine(targetFolder, truncatedManagedAssembly ? "Truncated.Managed.dll" : "e_sqlite3.dll");
            File.WriteAllBytes(invalidAssemblyPath, truncatedManagedAssembly ? File.ReadAllBytes(TestAssemblyPath).Take(512).ToArray() : CreateNativePortableExecutable());
            var options = CreateGenerateOptions(
                directory,
                WriteMapFile(directory, [Mapping(typeof(IOptionalConstantsProxy), typeof(OptionalConstantsTarget))]),
                [targetFolder]);

            var result = DuckTypeAotMappingResolver.Resolve(options);

            result.Errors.Should().BeEmpty();
            result.Mappings.Should().ContainSingle();
            if (truncatedManagedAssembly)
            {
                // A corrupt managed assembly is reported, so a mapping that needs it still shows the root cause.
                result.Warnings.Should().ContainSingle(warning => warning.Contains(invalidAssemblyPath));
            }
            else
            {
                result.Warnings.Should().BeEmpty();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private static string TestAssemblyPath => typeof(DuckTypeAotAdditionalParityTests).Assembly.Location;

    private static void WithGeneratedRegistry(Action assertions, params DuckTypeAotMapping[] mappings)
        => WithGeneratedRegistry(assertions, compatibilityAssertions: null, mappings);

    private static void WithGeneratedRegistry(Action assertions, Action<DuckTypeAotCompatibilityMatrix>? compatibilityAssertions, params DuckTypeAotMapping[] mappings)
    {
        var directory = CreateTemporaryDirectory();
        var loadContext = new AssemblyLoadContext("AdditionalParity", isCollectible: true);
        try
        {
            var options = CreateGenerateOptions(directory, WriteMapFile(directory, mappings), targetFolders: []);
            DuckTypeAotGenerateProcessor.Process(options).Should().Be(0);
            if (compatibilityAssertions is not null)
            {
                compatibilityAssertions(JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(options.OutputPath + ".compat.json"))!);
            }

            DuckType.ResetRuntimeModeForTests();
#if NETCOREAPP2_1
            var registry = Assembly.Load(File.ReadAllBytes(options.OutputPath));
#else
            using var registryStream = File.OpenRead(options.OutputPath);
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

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-additional-aot-parity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteMapFile(string directory, IEnumerable<DuckTypeAotMapping> mappings)
    {
        var mapPath = Path.Combine(directory, "map.json");
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
        return mapPath;
    }

    private static DuckTypeAotGenerateOptions CreateGenerateOptions(string directory, string mapPath, IReadOnlyList<string> targetFolders)
    {
        var outputPath = Path.Combine(directory, "Registry.dll");
        return new DuckTypeAotGenerateOptions(
            proxyAssemblies: [TestAssemblyPath],
            targetAssemblies: [TestAssemblyPath],
            targetFolders: targetFolders,
            targetFilters: ["*.dll"],
            mapFile: mapPath,
            genericInstantiationsFile: null,
            outputPath: outputPath,
            assemblyName: "Registry",
            trimmerDescriptorPath: outputPath + ".linker.xml",
            propsPath: outputPath + ".props");
    }

    private static byte[] CreateNativePortableExecutable()
    {
        // Smallest PE32 image with a DOS header, PE signature, COFF header and an optional header whose data directories
        // are all empty: like a native library, it has no CLI header.
        var image = new byte[0x40 + 4 + 20 + 224];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(image, 0x3C);
        image[0x40] = (byte)'P';
        image[0x41] = (byte)'E';
        BitConverter.GetBytes((ushort)0x14C).CopyTo(image, 0x44); // Machine: i386
        BitConverter.GetBytes((ushort)224).CopyTo(image, 0x54); // SizeOfOptionalHeader
        BitConverter.GetBytes((ushort)0x2102).CopyTo(image, 0x56); // Characteristics: executable, 32-bit, DLL
        BitConverter.GetBytes((ushort)0x10B).CopyTo(image, 0x58); // PE32 magic
        BitConverter.GetBytes(16).CopyTo(image, 0x58 + 92); // NumberOfRvaAndSizes
        return image;
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

    public interface ICommaSeparatedExplicitInterfaceProxy
    {
        [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+IExplicitFirst,Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+IExplicitSecond")]
        string Explicit();
    }

    public interface IExplicitFirst
    {
        string Explicit();
    }

    public interface IExplicitSecond
    {
        string Explicit();
    }

    public class ExplicitInterfaceTarget : IExplicitFirst
    {
        string IExplicitFirst.Explicit() => "first";
    }

    public interface IExplicitEcho
    {
        string Echo(string value);
    }

    public interface IExplicitEchoObjectProxy
    {
        [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+IExplicitEcho")]
        string Echo(object value);
    }

    public interface IExplicitEchoStringProxy
    {
        [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+IExplicitEcho")]
        string Echo(string value);
    }

    public class ExplicitAndPublicEchoTarget : IExplicitEcho
    {
        public string Echo(string value) => "public:" + value;

        string IExplicitEcho.Echo(string value) => "explicit:" + value;
    }

    public class NullableOptionalConstantsTarget
    {
        public string Format(int? count = 5, DayOfWeek? day = DayOfWeek.Friday, in int value = 3, int? none = null)
            => string.Join("|", count, day, value, none is null);
    }

    public class NegativeOptionalConstantsTarget
    {
        public string Format(
            int number = -5,
            sbyte small = -3,
            short medium = -100,
            uint large = 0xFFFFFFFB,
            long wide = -2,
            DayOfWeek day = (DayOfWeek)(-2),
            decimal amount = -1.5m,
            int? nullable = -7,
            in int byRef = -9,
            byte octet = 200)
            => string.Join("|", number, small, medium, large, wide, (int)day, amount.ToString(CultureInfo.InvariantCulture), nullable, byRef, octet);
    }

    public class VirtualInOptionalConstantsTarget
    {
        // `in` parameters of virtual methods are by-refs wrapped in modreq(InAttribute).
        public virtual string Format(in int value = 3, in string text = "text", in Guid id = default)
            => string.Join("|", value, text, id);
    }

    public class MissingOptionalObjectTarget
    {
        public string Format([Optional] object? missing, [Optional] int number, [Optional] string? text)
            => string.Join("|", ReferenceEquals(missing, Type.Missing) ? "Type.Missing" : missing?.ToString() ?? "null", number, text ?? "null");
    }

    public interface IGenericInOptionalProxy
    {
        string Describe<T>();
    }

    public class GenericInOptionalTarget
    {
        public string Describe<T>(in T value = default!) => value?.ToString() ?? "null";
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

    public struct MultiFieldStruct
    {
        public long A;
        public long B;
        public string? C;

        public override string ToString() => $"{A},{B},{C ?? "null"}";
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
