// <copyright file="DuckTypeAotAdditionalParityTests.Failures.cs" company="Datadog">
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
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Scenarios where the AOT registry has to fail, or behave, like dynamic duck typing: same exception type and message,
/// same ToString, same reverse implementation selection.
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Fact]
    public void GeneratedRegistryShouldReplayProxyTypeCreationFailuresWithoutBreakingOtherMappings()
    {
        // Reflection.Emit can't create these proxy types: dynamic duck typing fails with a DuckTypeException when it creates
        // them. Generating them would make the whole registry fail to load (and a NativeAOT application exit before Main), so
        // the registry replays the dynamic failure instead, and the other mappings keep working.
        var scenarios = new Dictionary<string, Func<object?>>
        {
            ["sealed-proxy"] = () => DuckType.Create<SealedNameProxy>(new BaseNamedTarget()),
            ["sealed-reverse-base"] = () => DuckType.CreateReverse(typeof(SealedReverseBase), new UnannotatedDelegation()),
            ["ignored-abstract-interface-member"] = () => DuckType.Create<IIgnoredAbstractMemberProxy>(new BaseNamedTarget()),
            ["ignored-abstract-class-member"] = () => DuckType.Create<IgnoredAbstractMemberProxy>(new BaseNamedTarget()),
            ["working-mapping"] = () => DuckType.Create<INamedProxy>(new BaseNamedTarget())!.Name,
        };

        DuckType.ResetRuntimeModeForTests();
        var dynamicOutcomes = scenarios.ToDictionary(scenario => scenario.Key, scenario => CaptureFailure(scenario.Value));
        dynamicOutcomes["working-mapping"].Should().Be("base");
        dynamicOutcomes.Where(outcome => outcome.Key != "working-mapping")
                       .Should()
                       .OnlyContain(outcome => outcome.Value.Contains("DuckTypeException:Error creating duck type for type: "));

        WithGeneratedRegistry(
            () => scenarios.ToDictionary(scenario => scenario.Key, scenario => CaptureFailure(scenario.Value)).Should().Equal(dynamicOutcomes),
            matrix => matrix.Mappings.Where(mapping => mapping.Status != DuckTypeAotCompatibilityStatuses.Compatible)
                            .Should()
                            .HaveCount(4)
                            .And.OnlyContain(mapping => mapping.DiagnosticCode == "DTAOT0215" && mapping.DynamicFailureReplayed),
            Mapping(typeof(SealedNameProxy), typeof(BaseNamedTarget)),
            Mapping(typeof(SealedReverseBase), typeof(UnannotatedDelegation), reverse: true),
            Mapping(typeof(IIgnoredAbstractMemberProxy), typeof(BaseNamedTarget)),
            Mapping(typeof(IgnoredAbstractMemberProxy), typeof(BaseNamedTarget)),
            Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)));
    }

    [Theory]
    // Reflection.Emit can't create these proxy types: dynamic duck typing fails when it creates the type (its dry run doesn't
    // see it), and the registry replays that failure instead of generating a type it can't load either, or one that works.
    [InlineData("interface-event")]
    [InlineData("abstract-class-event")]
    [InlineData("internal-abstract-member")]
    [InlineData("in-parameter")]
    [InlineData("new-property-of-another-type")]
    // The ToString the proxy copies from the target overrides the sealed ToString of the proxy class (the type load failure
    // has another message format, still about the proxy type).
    [InlineData("sealed-tostring-proxy")]
    // An abstract member left without implementation, for a target whose name makes the name of the proxy type longer than
    // the limit: dynamic duck typing truncates it, and the type load failure is still about the proxy type.
    [InlineData("truncated-proxy-type-name")]
#if NET5_0_OR_GREATER
    [InlineData("covariant-return")]
#endif
#if NET7_0_OR_GREATER
    [InlineData("static-abstract-member")]
#endif
    public void GeneratedRegistryShouldReplayTheFailuresOfProxyTypesReflectionEmitCantCreate(string scenario)
    {
        var (mapping, exercise) = scenario switch
        {
            "interface-event" => (Mapping(typeof(IEventProxy), typeof(EventTarget)), (Func<object?>)(() => DuckType.Create<IEventProxy>(new EventTarget())!.Name)),
            "abstract-class-event" => (Mapping(typeof(AbstractEventProxy), typeof(EventTarget)), () => DuckType.Create<AbstractEventProxy>(new EventTarget())!.Name),
            "internal-abstract-member" => (Mapping(typeof(InternalAbstractProxy), typeof(BaseNamedTarget)), () => DuckType.Create<InternalAbstractProxy>(new BaseNamedTarget())!.GetName()),
            "in-parameter" => (Mapping(typeof(IInParameterProxy), typeof(InParameterTarget)), () => DuckType.Create<IInParameterProxy>(new InParameterTarget())!.Echo(5)),
            "sealed-tostring-proxy" => (Mapping(typeof(SealedToStringProxy), typeof(ToStringTarget)), () => DuckType.Create<SealedToStringProxy>(new ToStringTarget())!.Name),
            "truncated-proxy-type-name" => (Mapping(typeof(IgnoredAbstractMemberProxy), typeof(LongNamedTarget<LongNamedArgumentNumberOneWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberTwoWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberThreeWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberFourWithANameLongEnoughToMakeTheProxyTypeNameLonger>)),
                                            () => DuckType.Create<IgnoredAbstractMemberProxy>(new LongNamedTarget<LongNamedArgumentNumberOneWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberTwoWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberThreeWithANameLongEnoughToMakeTheProxyTypeNameLonger, LongNamedArgumentNumberFourWithANameLongEnoughToMakeTheProxyTypeNameLonger>())!.Name),
#if NET5_0_OR_GREATER
            "covariant-return" => (Mapping(typeof(CovariantDerivedProxy), typeof(CovariantTarget)), () => DuckType.Create<CovariantDerivedProxy>(new CovariantTarget())!.Get()),
#endif
#if NET7_0_OR_GREATER
            "static-abstract-member" => (Mapping(typeof(IStaticAbstractProxy), typeof(BaseNamedTarget)), () => DuckType.Create(typeof(IStaticAbstractProxy), new BaseNamedTarget())),
#endif
            _ => (Mapping(typeof(INewTypedValueProxy), typeof(NewTypedValueTarget)), () => DuckType.Create<INewTypedValueProxy>(new NewTypedValueTarget())!.Value),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        expected.Should().Contain("DuckTypeException:Error creating duck type for type: ");

        WithGeneratedRegistry(
            () =>
            {
                CaptureFailure(exercise).Should().Be(expected);
                DuckType.Create<INamedProxy>(new BaseNamedTarget())!.Name.Should().Be("base", "the other mappings of the registry keep working");
            },
            matrix => matrix.Mappings.Should().ContainSingle(entry => entry.Status != DuckTypeAotCompatibilityStatuses.Compatible)
                            .Which.DynamicFailureReplayed.Should().BeTrue(),
            mapping,
            Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)));
    }

    [Theory]
    // The proxy type dynamic duck typing creates takes from the target the attributes of its ToString (here abstract) and the
    // methods it marks with [DuckInclude] (here overriding a sealed ToString): the same proxy type is created for the first
    // target (named to come first), not for these.
    [InlineData("abstract-tostring-target")]
    [InlineData("duck-include-target")]
    public void GeneratedRegistryShouldReplayCreationFailuresThatDependOnTheTarget(string scenario)
    {
        var (mappings, exercise, working) = scenario switch
        {
            "abstract-tostring-target" => (
                new[] { Mapping(typeof(INamedProxy), typeof(ShapeAPlainTarget)), Mapping(typeof(INamedProxy), typeof(ShapeBAbstractToStringTarget)) },
                (Func<object?>)(() => DuckType.CreateCache<INamedProxy>.CreateFrom<ShapeBAbstractToStringTarget>(new ShapeBAbstractToStringTargetImpl())!.Name),
                (Func<object?>)(() => DuckType.Create<INamedProxy>(new ShapeAPlainTarget())!.Name)),
            _ => (
                new[] { Mapping(typeof(SealedToStringProxy), typeof(ShapeAPlainTarget)), Mapping(typeof(SealedToStringProxy), typeof(ShapeBDuckIncludeTarget)) },
                () => DuckType.Create<SealedToStringProxy>(new ShapeBDuckIncludeTarget())!.ToString(),
                () => DuckType.Create<SealedToStringProxy>(new ShapeAPlainTarget())!.ToString()),
        };

        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        expected.Should().Contain("DuckTypeException:Error creating duck type for type: ");
        var expectedWorking = CaptureFailure(working);
        expectedWorking.Should().NotStartWith("throws:");

        WithGeneratedRegistry(
            () =>
            {
                CaptureFailure(exercise).Should().Be(expected);
                CaptureFailure(working).Should().Be(expectedWorking);
            },
            matrix => matrix.Mappings.Should().ContainSingle(entry => entry.Status != DuckTypeAotCompatibilityStatuses.Compatible)
                            .Which.DynamicFailureReplayed.Should().BeTrue(),
            mappings);
    }

    [Theory]
    // A [DuckIgnore] abstract member another member implements: a default implementation of a derived proxy interface, a
    // redeclaration with `new` (its implementation implements both), a [DuckReverseMethod] method, or an override that
    // inherits the [DuckIgnore].
#if NETCOREAPP3_0_OR_GREATER
    [InlineData("implemented-by-default-interface-method", "base|dim")]
#endif
    [InlineData("redeclared-with-new", "target-m")]
    [InlineData("reverse-implementation", "reverse:x")]
    [InlineData("override-inherits-ignore", "base|derived-ignored")]
    public void GeneratedRegistryShouldCreateProxiesWithIgnoredAbstractMembersLikeDynamicMode(string scenario, string expected)
    {
        var (mapping, exercise) = scenario switch
        {
#if NETCOREAPP3_0_OR_GREATER
            "implemented-by-default-interface-method" => (Mapping(typeof(IDimDerivedProxy), typeof(BaseNamedTarget)), (Func<object?>)(() =>
            {
                var proxy = DuckType.Create<IDimDerivedProxy>(new BaseNamedTarget())!;
                return proxy.Name + "|" + ((IDimBaseProxy)proxy).Ignored;
            })),
#endif
            "redeclared-with-new" => (Mapping(typeof(IRedeclaringProxy), typeof(RedeclaredTarget)), (Func<object?>)(() => DuckType.Create<IRedeclaringProxy>(new RedeclaredTarget())!.M())),
            "reverse-implementation" => (Mapping(typeof(ReverseIgnoredBase), typeof(ReverseIgnoredDelegation), reverse: true), () => ((ReverseIgnoredBase)DuckType.CreateReverse(typeof(ReverseIgnoredBase), new ReverseIgnoredDelegation())).Echo("x")),
            _ => (Mapping(typeof(DerivedImplementsIgnored), typeof(BaseNamedTarget)), () =>
            {
                var proxy = DuckType.Create<DerivedImplementsIgnored>(new BaseNamedTarget())!;
                return proxy.Name + "|" + proxy.Ignored;
            }),
        };

        AssertSameOutcome(expected, exercise, mapping);
    }

    [Fact]
    public void GeneratedRegistryShouldReplayTheDynamicFailureOfEachTarget()
    {
        // Dynamic duck typing creates the properties before the methods: a proxy with a missing property and an ambiguous
        // method fails on the property. And each target replays its own failure, even with the same proxy.
        Func<object?> missingProperty = () => DuckType.Create<IOrderedFailureProxy>(new AmbiguousMethodTarget());
        Func<object?> missingMethod = () => DuckType.Create<IOrderedFailureProxy>(new NamedWithoutMethodTarget());

        DuckType.ResetRuntimeModeForTests();
        var expectedMissingProperty = CaptureFailure(missingProperty);
        var expectedMissingMethod = CaptureFailure(missingMethod);
        expectedMissingProperty.Should().StartWith("throws:DuckTypePropertyOrFieldNotFoundException:");
        expectedMissingMethod.Should().StartWith("throws:DuckTypeTargetMethodNotFoundException:");

        WithGeneratedRegistry(
            () =>
            {
                CaptureFailure(missingProperty).Should().Be(expectedMissingProperty);
                CaptureFailure(missingMethod).Should().Be(expectedMissingMethod);
            },
            matrix => matrix.Mappings.Should().HaveCount(2).And.OnlyContain(mapping => mapping.DynamicFailureReplayed),
            Mapping(typeof(IOrderedFailureProxy), typeof(AmbiguousMethodTarget)),
            Mapping(typeof(IOrderedFailureProxy), typeof(NamedWithoutMethodTarget)));
    }

    [Fact]
    public void CompatibilityVerificationShouldOnlyAcceptReplayedDynamicFailures()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var assemblyName = typeof(DuckTypeAotAdditionalParityTests).Assembly.GetName().Name!;
            var replayedFailure = Mapping(typeof(IOrderedFailureProxy), typeof(NamedWithoutMethodTarget));
            Verify(replayedFailure).Should().Be(0, "the registry fails like dynamic duck typing");

            // A target the generator can't find isn't a dynamic duck typing failure.
            var missingTarget = new DuckTypeAotMapping(
                typeof(INamedProxy).FullName!,
                assemblyName,
                typeof(INamedProxy).Namespace + ".MissingTargetType",
                assemblyName,
                DuckTypeAotMappingMode.Forward,
                DuckTypeAotMappingSource.MapFile);
            Verify(replayedFailure, missingTarget).Should().Be(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        int Verify(params DuckTypeAotMapping[] mappings)
        {
            var mapPath = WriteMapFile(directory, mappings);
            var options = CreateGenerateOptions(directory, mapPath, targetFolders: []);
            DuckTypeAotGenerateProcessor.Process(options).Should().Be(0);
            return DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: options.OutputPath + ".compat.md",
                    compatMatrixPath: options.OutputPath + ".compat.json",
                    mapFilePath: mapPath,
                    failureMode: DuckTypeAotFailureMode.Strict));
        }
    }

    [Theory]
    // The target overrides object.ToString: the proxy forwards it.
    [InlineData("overridden", "target")]
    // The target doesn't override object.ToString: the proxy describes itself, like any object.
    [InlineData("inherited", "proxy")]
    // A ToString that hides object.ToString isn't an override either.
    [InlineData("hidden", "proxy")]
    // An interface target has no ToString to forward.
    [InlineData("interface-target", "proxy")]
    public void GeneratedRegistryShouldForwardToStringLikeDynamicMode(string scenario, string expected)
    {
        var (mapping, exercise) = scenario switch
        {
            "overridden" => (Mapping(typeof(INamedProxy), typeof(ToStringTarget)), (Func<object?>)(() => DescribeToString(DuckType.Create<INamedProxy>(new ToStringTarget())))),
            "inherited" => (Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)), () => DescribeToString(DuckType.Create<INamedProxy>(new BaseNamedTarget()))),
            "hidden" => (Mapping(typeof(INamedProxy), typeof(HiddenToStringTarget)), () => DescribeToString(DuckType.Create<INamedProxy>(new HiddenToStringTarget()))),
            _ => (Mapping(typeof(INamedProxy), typeof(INamedTarget)), () => DescribeToString(DuckType.CreateCache<INamedProxy>.CreateFrom<INamedTarget>(new ToStringTarget()))),
        };

        AssertSameOutcome(expected, exercise, mapping);

        static string? DescribeToString(object? proxy)
            => proxy?.ToString() == proxy?.GetType().ToString() ? "proxy" : proxy?.ToString();
    }

    [Fact]
    public void GeneratedRegistryShouldOverrideObjectMembersDeclaredByTheProxyInterface()
    {
        // The proxy methods implementing an Equals or GetHashCode declared by the proxy interface also override the object
        // methods: object.Equals and GetHashCode (e.g. dictionary keys) call the target.
        AssertSameOutcome(
            "True|4242",
            () =>
            {
                object proxy = DuckType.Create<IHashProxy>(new HashTarget())!;
                return FormattableString.Invariant($"{proxy.Equals("other")}|{proxy.GetHashCode()}");
            },
            Mapping(typeof(IHashProxy), typeof(HashTarget)));
    }

#if NETCOREAPP3_0_OR_GREATER
    [Fact]
    public void GeneratedRegistryShouldIgnorePrivateInterfaceMembers()
    {
        // A private interface member (a C# 8 default implementation helper) isn't part of the proxy contract.
        AssertSameOutcome(
            "base",
            () => DuckType.Create<IPrivateHelperProxy>(new BaseNamedTarget())!.Name,
            Mapping(typeof(IPrivateHelperProxy), typeof(BaseNamedTarget)));
    }
#endif

    [Theory]
    // [DuckReverseMethod] names honor BindingFlags.IgnoreCase.
    [InlineData("ignore-case", "ignore-case:x")]
    // ParameterTypeNames are resolved as type names, so assembly qualified generic arguments match too.
    [InlineData("qualified-parameter-type-names", "qualified:1")]
    // A [DuckReverseMethod] M(object) only overrides M(object): the M(string) overload keeps its base implementation.
    [InlineData("overload-not-paired", "base-string|impl")]
    public void GeneratedRegistryShouldSelectReverseImplementationsLikeDynamicMode(string scenario, string expected)
    {
        var (mapping, exercise) = scenario switch
        {
            "ignore-case" => (Mapping(typeof(IReverseEchoContract), typeof(IgnoreCaseReverseDelegation), reverse: true),
                              (Func<object?>)(() => ((IReverseEchoContract)DuckType.CreateReverse(typeof(IReverseEchoContract), new IgnoreCaseReverseDelegation())).Echo("x"))),
            "qualified-parameter-type-names" => (Mapping(typeof(IReverseCountContract), typeof(QualifiedParameterTypeNamesDelegation), reverse: true),
                                                 () => ((IReverseCountContract)DuckType.CreateReverse(typeof(IReverseCountContract), new QualifiedParameterTypeNamesDelegation())).Count(["a"])),
            _ => (Mapping(typeof(ReverseOverloadBase), typeof(ReverseOverloadImplementation), reverse: true), () =>
            {
                var proxy = (ReverseOverloadBase)DuckType.CreateReverse(typeof(ReverseOverloadBase), new ReverseOverloadImplementation());
                return proxy.M("x") + "|" + proxy.M((object)"x");
            }),
        };

        AssertSameOutcome(expected, exercise, mapping);
    }

    [Fact]
    public void GeneratedRegistryShouldReplayButNotVouchForFailuresOfTypesTheGeneratorCantFind()
    {
        // A type named in a duck attribute, in an assembly that isn't one of the generator's inputs: dynamic duck typing doesn't
        // find it in the generator, but the application may have it. The registry replays the failure, without marking it as a
        // dynamic duck typing failure, so the gates report it.
        Func<object?> exercise = () => DuckType.Create<IMissingTypeNameProxy>(new GenericEchoTarget());
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        expected.Should().Be("throws:DuckTypeException:Type not found: Not.A.Real.Type, Not.A.Real.Assembly");

        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().Be(expected),
            matrix => matrix.Mappings.Should().ContainSingle()
                            .Which.Should().Match<DuckTypeAotCompatibilityMapping>(entry => entry.Status != DuckTypeAotCompatibilityStatuses.Compatible && !entry.DynamicFailureReplayed),
            Mapping(typeof(IMissingTypeNameProxy), typeof(GenericEchoTarget)));
    }

    [Theory]
    // Two [DuckReverseMethod] properties implement the same property: like dynamic duck typing, the first one does.
    [InlineData("interface-base")]
    [InlineData("abstract-class-base")]
    public void GeneratedRegistryShouldSelectReversePropertyImplementationsLikeDynamicMode(string scenario)
    {
        var baseType = scenario == "interface-base" ? typeof(IReverseValueContract) : typeof(ReverseValueBase);
        Func<object?> exercise = () => baseType == typeof(IReverseValueContract)
                                           ? ((IReverseValueContract)DuckType.CreateReverse(baseType, new TwoReversePropertiesDelegation())).Value
                                           : ((ReverseValueBase)DuckType.CreateReverse(baseType, new TwoReversePropertiesDelegation())).Value;

        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        if (scenario == "interface-base")
        {
            expected.Should().Be("a");
        }

        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().Be(expected),
            Mapping(baseType, typeof(TwoReversePropertiesDelegation), reverse: true));
    }

    [Fact]
    public void GeneratedRegistryShouldAccessNonPublicGenericArgumentsOfOtherAssemblies()
    {
        // DuckTypeAotMappingMode is internal to the runner, which is neither a proxy nor a target assembly: like dynamic duck
        // typing, the registry has to ignore the access checks of every assembly it references.
        AssertSameOutcome(
            "Reverse",
            () => DuckType.Create<IBoxProxy<DuckTypeAotMappingMode>>(new Box<DuckTypeAotMappingMode>(DuckTypeAotMappingMode.Reverse))!.Value,
            Mapping(typeof(IBoxProxy<DuckTypeAotMappingMode>), typeof(Box<DuckTypeAotMappingMode>)));
    }

    [Fact]
    public void GeneratedTrimmerDescriptorShouldOnlyRootTypeDefinitionsByName()
    {
        // ILLink descriptors only resolve type definitions, with '/' between nested types: closed generic names fail (IL2008),
        // so closed generics aren't rooted by name.
        var directory = CreateTemporaryDirectory();
        try
        {
            var mappings = new[] { Mapping(typeof(INamedProxy), typeof(BaseNamedTarget)), Mapping(typeof(IBoxProxy<string>), typeof(Box<string>)) };
            var options = CreateGenerateOptions(directory, WriteMapFile(directory, mappings), targetFolders: []);
            DuckTypeAotGenerateProcessor.Process(options).Should().Be(0);

            var descriptor = File.ReadAllText(options.TrimmerDescriptorPath);
            descriptor.Should().Contain("<type fullname=\"Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests/INamedProxy\" preserve=\"all\" />");
            descriptor.Should().Contain("<type fullname=\"Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests/BaseNamedTarget\" preserve=\"all\" />");
            descriptor.Should().NotContain("Box`1").And.NotContain("[[").And.NotContain("+");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Captures the outcome of a scenario: the returned value, or the exception types (outermost first) up to the first
    /// DuckTypeException with its message, prefixed with "throws:". Inner exceptions of a DuckTypeException aren't
    /// compared: the registry replays the exception dynamic duck typing throws, not the Reflection.Emit failure inside it.
    /// </summary>
    private static string CaptureFailure(Func<object?> exercise)
    {
        try
        {
            return Convert.ToString(exercise(), CultureInfo.InvariantCulture) ?? "null";
        }
        catch (Exception ex)
        {
            var exceptionTypes = new List<string>();
            for (Exception? current = ex; current is not null; current = current.InnerException)
            {
                exceptionTypes.Add(current.GetType().Name);
                if (current is DuckTypeException)
                {
                    return "throws:" + string.Join(">", exceptionTypes) + ":" + current.Message;
                }
            }

            return "throws:" + string.Join(">", exceptionTypes);
        }
    }

    public sealed class SealedNameProxy
    {
        public string Name => "sealed";
    }

    public sealed class SealedReverseBase
    {
    }

    public interface IIgnoredAbstractMemberProxy
    {
        string Name { get; }

        [DuckIgnore]
        string Ignored { get; }
    }

    public class LongNamedTarget<T1, T2, T3, T4>
    {
        public string Name => "long";
    }

    public class LongNamedArgumentNumberOneWithANameLongEnoughToMakeTheProxyTypeNameLonger
    {
    }

    public class LongNamedArgumentNumberTwoWithANameLongEnoughToMakeTheProxyTypeNameLonger
    {
    }

    public class LongNamedArgumentNumberThreeWithANameLongEnoughToMakeTheProxyTypeNameLonger
    {
    }

    public class LongNamedArgumentNumberFourWithANameLongEnoughToMakeTheProxyTypeNameLonger
    {
    }

    public abstract class IgnoredAbstractMemberProxy
    {
        public abstract string Name { get; }

        [DuckIgnore]
        public abstract string Ignored { get; }
    }

    public interface IOrderedFailureProxy
    {
        string Go(object value);

        string Name { get; }
    }

    public class AmbiguousMethodTarget
    {
        public string Go(string value) => "string";

        public string Go(Uri value) => "uri";
    }

    public class NamedWithoutMethodTarget
    {
        public string Name => "name";
    }

    public interface INamedTarget
    {
        string Name { get; }
    }

    public class ToStringTarget : INamedTarget
    {
        public string Name => "name";

        public override string ToString() => "target";
    }

    public interface IMissingTypeNameProxy
    {
        [Duck(GenericParameterTypeNames = new[] { "Not.A.Real.Type, Not.A.Real.Assembly" })]
        string Echo(string value);
    }

    public class GenericEchoTarget
    {
        public string Echo<T>(string value) => value;
    }

    public interface IReverseValueContract
    {
        string Value { get; }
    }

    public abstract class ReverseValueBase
    {
        public abstract string Value { get; }
    }

    public class TwoReversePropertiesDelegation
    {
        [DuckReverseMethod]
        public string Value => "a";

        [DuckReverseMethod(Name = "Value")]
        public string Other => "b";
    }

    public class HiddenToStringTarget
    {
        public string Name => "name";

        public new string ToString() => "hidden";
    }

    public abstract class SealedToStringProxy
    {
        public abstract string Name { get; }

        public sealed override string ToString() => "proxy:" + Name;
    }

    public class ShapeAPlainTarget
    {
        public string Name => "plain";
    }

    public abstract class ShapeBAbstractToStringTarget
    {
        public string Name => "abstract-tostring";

        public abstract override string ToString();
    }

    public class ShapeBAbstractToStringTargetImpl : ShapeBAbstractToStringTarget
    {
        public override string ToString() => "impl";
    }

    public class ShapeBDuckIncludeTarget
    {
        public string Name => "include";

        [DuckInclude]
        public override string ToString() => "target-tostring";
    }

    public interface IHashProxy
    {
        string Name { get; }

        bool Equals(object? obj);

        int GetHashCode();
    }

    public class HashTarget
    {
        public string Name => "hash";

        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 4242;
    }

#if NETCOREAPP3_0_OR_GREATER
    public interface IPrivateHelperProxy
    {
        string Name { get; }

        private string Helper() => "helper";
    }
#endif

    public interface IReverseEchoContract
    {
        string Echo(string value);
    }

    public class IgnoreCaseReverseDelegation
    {
        [DuckReverseMethod(Name = "echo", BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)]
        public string Implementation(string value) => "ignore-case:" + value;
    }

    public interface IReverseCountContract
    {
        string Count(List<string> values);
    }

    public class QualifiedParameterTypeNamesDelegation
    {
        [DuckReverseMethod(ParameterTypeNames = new[] { "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]" })]
        public string Count(List<string> values) => FormattableString.Invariant($"qualified:{values.Count}");
    }

    public interface IEventProxy
    {
        string Name { get; }

        event EventHandler Changed;
    }

    public class EventTarget
    {
        public event EventHandler? Changed;

        public string Name => "event-target";

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    public abstract class AbstractEventProxy
    {
        public abstract event EventHandler Changed;

        public abstract string Name { get; }
    }

    public abstract class InternalAbstractProxy
    {
        public string GetName() => Name;

        internal abstract string Name { get; }
    }

    public interface IInParameterProxy
    {
        int Echo(in int value);
    }

    public class InParameterTarget
    {
        public int Echo(in int value) => value * 2;
    }

    public interface INewTypedValueBase
    {
        object Value { get; }
    }

    public interface INewTypedValueProxy : INewTypedValueBase
    {
        new string Value { get; }
    }

    public class NewTypedValueTarget
    {
        public string Value => "x";
    }

#if NET5_0_OR_GREATER
    public abstract class CovariantBaseProxy
    {
        public abstract object Get();
    }

    public abstract class CovariantDerivedProxy : CovariantBaseProxy
    {
        public abstract override string Get();
    }

    public class CovariantTarget
    {
        public string Get() => "covariant";
    }
#endif

#if NET7_0_OR_GREATER
    public interface IStaticAbstractProxy
    {
        string Name { get; }

        static abstract string Make();
    }
#endif

#if NETCOREAPP3_0_OR_GREATER
    public interface IDimBaseProxy
    {
        [DuckIgnore]
        string Ignored { get; }
    }

    public interface IDimDerivedProxy : IDimBaseProxy
    {
        string Name { get; }

        string IDimBaseProxy.Ignored => "dim";
    }
#endif

    public interface IRedeclaredBase
    {
        [DuckIgnore]
        string M();
    }

    public interface IRedeclaringProxy : IRedeclaredBase
    {
        new string M();
    }

    public class RedeclaredTarget
    {
        public string M() => "target-m";
    }

    public abstract class ReverseIgnoredBase
    {
        [DuckIgnore]
        public abstract string Echo(string value);
    }

    public class ReverseIgnoredDelegation
    {
        [DuckReverseMethod]
        public string Echo(string value) => "reverse:" + value;
    }

    public abstract class BaseWithIgnoredAbstract
    {
        public abstract string Name { get; }

        [DuckIgnore]
        public abstract string Ignored { get; }
    }

    public abstract class DerivedImplementsIgnored : BaseWithIgnoredAbstract
    {
        public override string Ignored => "derived-ignored";
    }

    public abstract class ReverseOverloadBase
    {
        public virtual string M(object value) => "base-object";

        public virtual string M(string value) => "base-string";
    }

    public class ReverseOverloadImplementation
    {
        [DuckReverseMethod]
        public string M(object value) => "impl";
    }
}
