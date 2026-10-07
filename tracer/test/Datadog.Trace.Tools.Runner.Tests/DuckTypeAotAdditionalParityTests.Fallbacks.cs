// <copyright file="DuckTypeAotAdditionalParityTests.Fallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The runtime types a registry can't name: the other array types of an array mapping, the arrays a mapping that isn't an
/// array type is assignable from, and the non-public types of the core library (registered as aliases when the generator knows
/// them, else served by a registered type they derive from or implement). Every scenario runs each exercise in dynamic mode,
/// then with the generated registry, and compares the outcomes (values, or exception types and messages).
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // A failure registered for an array type is thrown for the other array types with their own name in the message.
    [InlineData("failure-other-arrays")]
    [InlineData("failure-covariant-arrays")]
    [InlineData("failure-derived-element-arrays")]
    // A System.Array mapping binds what reflection finds on System.Array (an explicit interface implementation here), which
    // dynamic duck typing doesn't find on an array type: it serves System.Array only.
    [InlineData("system-array-only")]
    [InlineData("system-array-and-int-array")]
    // [DuckCopy] struct mappings serve the other array types too.
    [InlineData("duck-copy-int-array")]
    [InlineData("duck-copy-object-array")]
    // Mappings that aren't array types, but that arrays are assignable to.
    [InlineData("object-mapping-arrays")]
    [InlineData("enumerable-mapping-arrays")]
    // The activator of another array type casts the instance like dynamic duck typing's.
    [InlineData("activator-of-another-array-type")]
    // A reverse proxy with an array delegation type.
    [InlineData("reverse-array-delegation")]
    // Non-public types of the core library: RuntimeType for System.Type, the box of an async method for Task<T>.
    [InlineData("runtime-type")]
    [InlineData("runtime-type-failure")]
    [InlineData("async-method-task")]
    // Members only the non-public type has, or that it hides, re-implements or overrides (aliases of the non-public types).
    [InlineData("runtime-type-private-field")]
    [InlineData("runtime-method-info-internal-property")]
    [InlineData("runtime-method-info-to-string")]
    [InlineData("runtime-property-info-covariant-return")]
    [InlineData("sync-hashtable-explicit-reimplementation")]
    [InlineData("runtime-resource-set-hidden-private-method")]
    [InlineData("object-mapping-runtime-type")]
    [InlineData("duck-copy-runtime-type")]
    [InlineData("runtime-type-activator")]
    // Types the generator can't know (closed over a type of the runtime): the proxy of the closest registered type they derive
    // from or implement, whose failure isn't theirs.
    [InlineData("array-enumerator-interface-failure-and-success")]
    [InlineData("array-enumerator-derived-interface-failure")]
    [InlineData("array-enumerator-interface-to-string")]
    // Task<VoidTaskResult> (Task.CompletedTask, async methods returning Task) is an alias of Task mappings; the async boxes
    // and the other tasks derived from Task are served by the closest of them.
    [InlineData("completed-task")]
    [InlineData("async-method-void-task")]
    [InlineData("delay-task")]
    public void GeneratedRegistryShouldServeRuntimeTypesItCantNameLikeDynamicMode(string scenario)
    {
        var (exercises, mappings) = scenario switch
        {
            "failure-other-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new long[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new string[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1, 1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(int[])) }),
            "failure-covariant-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new object[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new string[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackElement[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new int[1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(object[])) }),
            "failure-derived-element-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackElement[1])!.Length,
                    () => DuckType.Create<IFallbackMissingProxy>(new FallbackDerivedElement[1])!.Length,
                },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(FallbackElement[])) }),
            "system-array-only" => (
                new Func<object?>[]
                {
                    () => DuckType.CreateCache<IFallbackExplicitCountProxy>.CreateFrom<Array>(new int[3])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new int[3])!.Count,
                    () => DuckType.CanCreate<IFallbackExplicitCountProxy>(new int[3]),
                },
                new[] { Mapping(typeof(IFallbackExplicitCountProxy), typeof(Array)) }),
            "system-array-and-int-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new int[3])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new long[4])!.Count,
                    () => DuckType.Create<IFallbackExplicitCountProxy>(new uint[5])!.Count,
                    () => DuckType.CreateCache<IFallbackExplicitCountProxy>.CreateFrom<Array>(new long[6])!.Count,
                },
                new[] { Mapping(typeof(IFallbackExplicitCountProxy), typeof(Array)), Mapping(typeof(IFallbackExplicitCountProxy), typeof(int[])) }),
            "duck-copy-int-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<FallbackLengthCopy>(new int[3]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new uint[4]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new long[5]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new int[2, 3]).Rank,
                    () => DuckType.Create<FallbackLengthCopy>(new string[6]).Length,
                },
                new[] { Mapping(typeof(FallbackLengthCopy), typeof(int[])) }),
            "duck-copy-object-array" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<FallbackLengthCopy>(new object[3]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new string[4]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new int[5]).Length,
                    () => DuckType.Create<FallbackLengthCopy>(new FallbackElement[6]).Length,
                },
                new[] { Mapping(typeof(FallbackLengthCopy), typeof(object[])) }),
            "object-mapping-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackEqualsProxy>(new FallbackElement())!.Equals(null),
                    () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(new int[1])),
                    () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(new string[1])),
                },
                new[] { Mapping(typeof(IFallbackEqualsProxy), typeof(object)) }),
            "enumerable-mapping-arrays" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackEnumerableProxy>(new ArrayList { 1 })!.GetEnumerator().MoveNext(),
                    () => DuckType.Create<IFallbackEnumerableProxy>(new[] { 1 })!.GetEnumerator().MoveNext(),
                    () => DescribeDuckType(DuckType.Create<IFallbackEnumerableProxy>(new[] { "a", "b" })),
                },
                new[] { Mapping(typeof(IFallbackEnumerableProxy), typeof(IEnumerable)) }),
            "activator-of-another-array-type" => (
                new Func<object?>[]
                {
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(long[])).CreateInstance<IFallbackLengthProxy>(new int[3])),
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(int[])).CreateInstance<IFallbackLengthProxy>(new long[3])),
                    () => DescribeDuckType(DuckType.GetOrCreateProxyType(typeof(IFallbackLengthProxy), typeof(long[])).CreateInstance<IFallbackLengthProxy>("not an array")),
                },
                new[] { Mapping(typeof(IFallbackLengthProxy), typeof(int[])) }),
            "reverse-array-delegation" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var reverse = DuckType.CreateReverse(typeof(FallbackVirtualNameBase), new int[2]);
                        return ((FallbackVirtualNameBase)reverse).Name + "|" + ((IDuckType)reverse).Type.Name;
                    },
                },
                new[] { Mapping(typeof(FallbackVirtualNameBase), typeof(int[]), reverse: true) }),
            "runtime-type" => (
                new Func<object?>[] { () => DescribeDuckType(DuckType.Create<IFallbackNameProxy>(typeof(string))) },
                new[] { Mapping(typeof(IFallbackNameProxy), typeof(Type)) }),
            "runtime-type-failure" => (
                new Func<object?>[] { () => DuckType.Create<IFallbackMissingProxy>(typeof(string))!.Missing },
                new[] { Mapping(typeof(IFallbackMissingProxy), typeof(Type)) }),
            "runtime-type-private-field" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackHandleProxy>(typeof(string))!.Handle != IntPtr.Zero,
                    () => DuckType.CanCreate<IFallbackHandleProxy>(typeof(string)),
                },
                new[] { Mapping(typeof(IFallbackHandleProxy), typeof(Type)) }),
            "runtime-method-info-internal-property" => (
                new Func<object?>[] { () => DuckType.Create<IFallbackBindingFlagsProxy>(typeof(object).GetMethod("ToString")!)!.BindingFlags.ToString() },
                new[] { Mapping(typeof(IFallbackBindingFlagsProxy), typeof(MethodInfo)) }),
            "runtime-method-info-to-string" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackNameProxy>(typeof(object).GetMethod("ToString")!)!.ToString(),
                    () => ((IDuckType)DuckType.Create<IFallbackNameProxy>(typeof(object).GetMethod("ToString")!)!).ToString(),
                    () => ((IDuckType)DuckType.Create<IFallbackIsDefinedProxy>(typeof(string))!).ToString(),
                },
                new[] { Mapping(typeof(IFallbackNameProxy), typeof(MethodInfo)), Mapping(typeof(IFallbackIsDefinedProxy), typeof(ICustomAttributeProvider)) }),
            "runtime-property-info-covariant-return" => (
                new Func<object?>[] { () => DuckType.Create<IFallbackGetGetMethodProxy>(typeof(string).GetProperty("Length")!)!.GetGetMethod(false)?.Name },
                new[] { Mapping(typeof(IFallbackGetGetMethodProxy), typeof(PropertyInfo)) }),
            "sync-hashtable-explicit-reimplementation" => (
                new Func<object?>[]
                {
                    () => CountItems(DuckType.Create<IFallbackExplicitEnumerableProxy>(new Hashtable { { 1, 1 }, { 2, 2 } })!.GetEnumerator()),
                    () => CountItems(DuckType.Create<IFallbackExplicitEnumerableProxy>(Hashtable.Synchronized(new Hashtable { { 1, 1 }, { 2, 2 } }))!.GetEnumerator()),
                },
                new[] { Mapping(typeof(IFallbackExplicitEnumerableProxy), typeof(Hashtable)) }),
            "runtime-resource-set-hidden-private-method" => (
                new Func<object?>[]
                {
                    () => GetCoreLibraryResourceSet().GetType().Name,
                    () => DuckType.Create<IFallbackEnumeratorHelperProxy>(GetCoreLibraryResourceSet())!.GetEnumeratorHelper().MoveNext(),
                },
                new[] { Mapping(typeof(IFallbackEnumeratorHelperProxy), typeof(ResourceSet)) }),
            "object-mapping-runtime-type" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackNameProxy>(new object())!.Name,
                    () => DuckType.Create<IFallbackNameProxy>(typeof(string))!.Name,
                },
                new[] { Mapping(typeof(IFallbackNameProxy), typeof(object)) }),
            "duck-copy-runtime-type" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<FallbackNameCopy>(typeof(string)).Name,
                    () => DuckType.GetOrCreateProxyType(typeof(FallbackNameCopy), typeof(string).GetType()).CreateInstance<FallbackNameCopy>("not a type").Name,
                },
                new[] { Mapping(typeof(FallbackNameCopy), typeof(Type)) }),
            "runtime-type-activator" => (
                new Func<object?>[]
                {
                    () => DuckType.GetOrCreateProxyType(typeof(IFallbackNameProxy), typeof(string).GetType()).CreateInstance<IFallbackNameProxy>("not a type").Name,
                    () => DuckType.GetOrCreateProxyType(typeof(IFallbackNameProxy), typeof(string).GetType()).CreateInstance<IFallbackNameProxy>(typeof(Type).GetMethod("ToString")!).Name,
                    () => ((IFallbackNameProxy)DuckType.Create(typeof(IFallbackNameProxy), typeof(string))!).Name,
                    () => typeof(string).DuckAs<IFallbackNameProxy>()?.Name,
                    () => typeof(string).TryDuckCast<IFallbackNameProxy>(out var proxy) + ":" + proxy?.Name,
                },
                new[] { Mapping(typeof(IFallbackNameProxy), typeof(Type)) }),
            "array-enumerator-interface-failure-and-success" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        DuckType.Create<IFallbackDisposeProxy>(GetArrayEnumerator())!.Dispose();
                        return "disposed";
                    },
                },
                new[] { Mapping(typeof(IFallbackDisposeProxy), typeof(IEnumerator)), Mapping(typeof(IFallbackDisposeProxy), typeof(IDisposable)) }),
            "array-enumerator-derived-interface-failure" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        DuckType.Create<IFallbackDisposeProxy>(GetArrayEnumerator())!.Dispose();
                        return "disposed";
                    },
                },
                new[] { Mapping(typeof(IFallbackDisposeProxy), typeof(IEnumerator<int>)), Mapping(typeof(IFallbackDisposeProxy), typeof(IDisposable)) }),
            "array-enumerator-interface-to-string" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackMoveNextProxy>(GetArrayEnumerator())!.MoveNext(),
                    () => ((IDuckType)DuckType.Create<IFallbackMoveNextProxy>(GetArrayEnumerator())!).ToString(),
                    () => ((IDuckType)DuckType.Create<IFallbackMoveNextProxy>(GetArrayEnumerator())!).Type == GetArrayEnumerator().GetType(),
                },
                new[] { Mapping(typeof(IFallbackMoveNextProxy), typeof(IEnumerator)) }),
            "completed-task" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackContinueWithProxy>(Task.CompletedTask)!.ContinueWith(_ => 1).Result,
                    () => Convert.ToString(DuckType.Create<IFallbackObjectResultProxy>(Task.CompletedTask)!.Result, CultureInfo.InvariantCulture),
                    () => DuckType.Create<IFallbackIntResultProxy>(Task.CompletedTask)!.Result,
                },
                new[]
                {
                    Mapping(typeof(IFallbackContinueWithProxy), typeof(Task)),
                    Mapping(typeof(IFallbackObjectResultProxy), typeof(Task)),
                    Mapping(typeof(IFallbackIntResultProxy), typeof(Task)),
                }),
            "async-method-void-task" => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var task = FallbackVoidAsync();
                        return DuckType.Create<IFallbackContinueWithProxy>(task)!.ContinueWith(_ => 1).Result + "|" + (task.GetType().BaseType == Task.CompletedTask.GetType());
                    },
                    () => Convert.ToString(DuckType.Create<IFallbackObjectResultProxy>(FallbackVoidAsync())!.Result, CultureInfo.InvariantCulture),
                },
                new[] { Mapping(typeof(IFallbackContinueWithProxy), typeof(Task)), Mapping(typeof(IFallbackObjectResultProxy), typeof(Task)) }),
            "delay-task" => (
                new Func<object?>[]
                {
                    () => DuckType.Create<IFallbackContinueWithProxy>(Task.Delay(Timeout.Infinite, new CancellationTokenSource().Token)) is not null,
                    () => DuckType.CanCreate<IFallbackContinueWithProxy>(Task.Delay(Timeout.Infinite, new CancellationTokenSource().Token)),
                },
                new[] { Mapping(typeof(IFallbackContinueWithProxy), typeof(Task)) }),
            _ => (
                new Func<object?>[]
                {
                    () =>
                    {
                        var task = FallbackAsyncValueAsync();
                        task.Wait();
                        return DuckType.Create<IFallbackTaskResultProxy>(task)!.Result + "|" + (task.GetType() != typeof(Task<int>));
                    },
                },
                new[] { Mapping(typeof(IFallbackTaskResultProxy), typeof(Task<int>)) }),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = exercises.Select(CaptureFailure).ToArray();
        WithGeneratedRegistry(
            () => exercises.Select(CaptureFailure).Should().Equal(expected, "AOT duck typing should behave like dynamic duck typing"),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            mappings);
    }

    [Fact]
    public void GeneratedRegistryShouldReplayTheFailureOfTargetsWhoseNameDynamicDuckTypingCantUse()
    {
        // The dynamic assembly dynamic duck typing creates for a type that isn't visible is named after the type: an '=' in the
        // name (compiler-generated static array data) fails in every process, which the registry replays.
        var staticArrayDataType = typeof(DuckTypeAotAdditionalParityTests).Assembly.GetTypes().First(type => type.Name.StartsWith("__StaticArrayInitTypeSize=", StringComparison.Ordinal));
        Func<object?> exercise = () => DescribeDuckType(DuckType.Create<IFallbackEqualsProxy>(Activator.CreateInstance(staticArrayDataType)!));
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureWithInnerException(exercise);
        expected.Should().StartWith("throws:", "dynamic duck typing can't create this proxy");
        WithGeneratedRegistry(
            () => CaptureWithInnerException(exercise).Should().Be(expected, "AOT duck typing should behave like dynamic duck typing"),
            matrix => matrix.Mappings.Should().ContainSingle().Which.DynamicFailureReplayed.Should().BeTrue(),
            new DuckTypeAotMapping(typeof(IFallbackEqualsProxy).FullName!, typeof(IFallbackEqualsProxy).Assembly.GetName().Name!, staticArrayDataType.FullName!, staticArrayDataType.Assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile));
    }

    [Fact]
    public void GeneratedRegistryShouldEvaluateObjectMappingsWithDynamicDuckTypingWhenCoreLibIsATarget()
    {
        // Every type of the target assemblies derives from System.Object: the mapping is checked with dynamic duck typing, even
        // with the core library's types among its other runtime types.
        WithGeneratedRegistry(
            () => DuckType.Create<IFallbackEqualsProxy>(new FallbackElement())!.Equals(null).Should().BeFalse(),
            matrix => matrix.Mappings.Should().ContainSingle().Which.Should().Match<DuckTypeAotCompatibilityMapping>(mapping => mapping.Status == DuckTypeAotCompatibilityStatuses.Compatible && !mapping.CheckedAgainstMetadataOnly),
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(IFallbackEqualsProxy), typeof(object)));
    }

    [Fact]
    public void GeneratedRegistryShouldKeepTheWrappedExceptionOfFailuresOfOtherRuntimeTypes()
    {
        // Reflection.Emit can't create the proxy (an abstract event): the failure wraps a TypeLoadException, for the array type
        // of the mapping, another array type, System.Type and System.RuntimeType.
        var exercises = new Func<object?>[]
        {
            () => DuckType.Create<FallbackAbstractEventProxy>(new int[2])!.Length,
            () => DuckType.Create<FallbackAbstractEventProxy>(new long[3])!.Length,
            () => DuckType.Create<FallbackAbstractEventProxy>(typeof(Type))!.Length,
            () => DuckType.Create<FallbackAbstractEventProxy>(typeof(string))!.Length,
        };
        DuckType.ResetRuntimeModeForTests();
        var expected = exercises.Select(CaptureWithoutGeneratedNames).ToArray();
        expected[1].Should().Contain("[inner TypeLoadException");
        WithGeneratedRegistry(
            () => exercises.Select(CaptureWithoutGeneratedNames).Should().Equal(expected, "AOT duck typing should behave like dynamic duck typing"),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(FallbackAbstractEventProxy), typeof(int[])),
            Mapping(typeof(FallbackAbstractEventProxy), typeof(Type)));

        // The names of the types dynamic duck typing generates for the core library's types, in the wrapped exception's message.
        static string CaptureWithoutGeneratedNames(Func<object?> exercise)
            => Regex.Replace(CaptureWithInnerException(exercise), @"System_Private_CoreLib__[0-9A-F]+[^' ]*", "<generated>");
    }

    [Fact]
    public void GeneratedRegistryShouldNotServeRuntimeTypesWithTheFailureOfAnotherType()
    {
        // The registered types the runtime types derive from fail: the array enumerator (a closed generic type of the core
        // library) and the box of an async method aren't served with that failure, which would be the one of another type (dynamic
        // duck typing binds their own members). Without a registration of their own, they have none, named in the exception.
        var enumeratorType = GetArrayEnumerator().GetType();
        Func<object?> enumerator = () => DuckType.Create<IFallbackNameProxy>(GetArrayEnumerator())!.Name;
        Func<object?> box = () => DuckType.Create<IFallbackIntResultProxy>(FallbackVoidAsync())!.Result;
        DuckType.ResetRuntimeModeForTests();
        CaptureFailure(enumerator).Should().StartWith("throws:DuckTypePropertyOrFieldNotFoundException:");
        CaptureFailure(box).Should().StartWith("throws:DuckType");
        WithGeneratedRegistry(
            () =>
            {
                CaptureFailure(enumerator).Should().Be($"throws:DuckTypeAotMissingProxyRegistrationException:AOT duck typing mapping not found for proxy '{typeof(IFallbackNameProxy).FullName}' and target '{enumeratorType.FullName}' (reverse=False).");
                CaptureFailure(box).Should().StartWith("throws:DuckTypeAotMissingProxyRegistrationException:");
            },
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            Mapping(typeof(IFallbackNameProxy), typeof(object)),
            Mapping(typeof(IFallbackIntResultProxy), typeof(Task)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedRegistryShouldServeClosedGenericTypesOverApplicationTypesThatAreListed(bool listed)
    {
        // A closed generic type of the core library over a non-public type of the application isn't a type of the runtime: the
        // registry names it when the generic instantiations list it, and has no registration for it otherwise.
        Func<object?> exercise = () => DuckType.Create<IFallbackCountProxy>(new List<FallbackHidden> { new() })!.Count;
        var mapping = Mapping(typeof(IFallbackCountProxy), typeof(IEnumerable<FallbackHidden>));
        if (listed)
        {
            AssertSameOutcomeWithInputs(
                exercise,
                [TestAssemblyPath, typeof(object).Assembly.Location],
                JsonConvert.SerializeObject(new[] { new { type = typeof(List<FallbackHidden>).FullName, assembly = typeof(List<>).Assembly.GetName().Name } }),
                mapping);
            return;
        }

        DuckType.ResetRuntimeModeForTests();
        CaptureFailure(exercise).Should().Be("1");
        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().StartWith("throws:DuckTypeAotMissingProxyRegistrationException:"),
            compatibilityAssertions: null,
            extraTargetAssemblies: [typeof(object).Assembly.Location],
            mapping);
    }

    private static IEnumerator GetArrayEnumerator() => ((IEnumerable<int>)new[] { 1, 2 }).GetEnumerator();

    private static int CountItems(IEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static ResourceSet GetCoreLibraryResourceSet()
    {
        var resourceName = typeof(object).Assembly.GetManifestResourceNames().First(name => name.EndsWith(".resources", StringComparison.Ordinal));
        var manager = new ResourceManager(resourceName.Substring(0, resourceName.Length - ".resources".Length), typeof(object).Assembly);
        return manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!;
    }

    private static async Task FallbackVoidAsync() => await Task.Delay(1).ConfigureAwait(false);

    private static string DescribeDuckType(object? proxy)
        => proxy is IDuckType duckType
               ? $"{duckType.Type.Name}|{duckType.Instance?.GetType().Name}|{(proxy is IFallbackLengthProxy lengthProxy ? lengthProxy.Length : -1)}"
               : "null";

    private static async Task<int> FallbackAsyncValueAsync()
    {
        await Task.Yield();
        return 42;
    }

    public interface IFallbackLengthProxy
    {
        int Length { get; }
    }

    public interface IFallbackMissingProxy
    {
        int Length { get; }

        int Missing { get; }
    }

    public interface IFallbackExplicitCountProxy
    {
        [Duck(ExplicitInterfaceTypeName = "System.Collections.ICollection")]
        int Count { get; }
    }

    public interface IFallbackEqualsProxy
    {
        bool Equals(object? obj);
    }

    public interface IFallbackEnumerableProxy
    {
        IEnumerator GetEnumerator();
    }

    public interface IFallbackNameProxy
    {
        string Name { get; }
    }

    public interface IFallbackTaskResultProxy
    {
        int Result { get; }
    }

    public interface IFallbackHandleProxy
    {
        [DuckField(Name = "m_handle")]
        IntPtr Handle { get; }
    }

    public interface IFallbackBindingFlagsProxy
    {
        BindingFlags BindingFlags { get; }
    }

    public interface IFallbackIsDefinedProxy
    {
        bool IsDefined(Type attributeType, bool inherit);
    }

    public interface IFallbackGetGetMethodProxy
    {
        MethodInfo? GetGetMethod(bool nonPublic);
    }

    public interface IFallbackExplicitEnumerableProxy
    {
        [Duck(ExplicitInterfaceTypeName = "System.Collections.IEnumerable")]
        IEnumerator GetEnumerator();
    }

    public interface IFallbackEnumeratorHelperProxy
    {
        IDictionaryEnumerator GetEnumeratorHelper();
    }

    public interface IFallbackDisposeProxy
    {
        void Dispose();
    }

    public interface IFallbackMoveNextProxy
    {
        bool MoveNext();
    }

    public interface IFallbackContinueWithProxy
    {
        Task<TResult> ContinueWith<TResult>(Func<Task, TResult> continuationFunction);
    }

    public interface IFallbackObjectResultProxy
    {
        object? Result { get; }
    }

    public interface IFallbackIntResultProxy
    {
        int Result { get; }
    }

    public interface IFallbackCountProxy
    {
        int Count { get; }
    }

    public abstract class FallbackAbstractEventProxy
    {
        public abstract event EventHandler Changed;

        public abstract int Length { get; }
    }

    [DuckCopy]
    public struct FallbackNameCopy
    {
        public string Name;
    }

    [DuckCopy]
    public struct FallbackLengthCopy
    {
        public int Length;
        public int Rank;
    }

    public class FallbackElement
    {
    }

    public class FallbackDerivedElement : FallbackElement
    {
    }

    public class FallbackVirtualNameBase
    {
        public virtual string Name => "base";
    }

    internal class FallbackHidden
    {
    }
}
