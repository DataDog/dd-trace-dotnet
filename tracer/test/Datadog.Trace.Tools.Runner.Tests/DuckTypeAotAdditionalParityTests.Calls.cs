// <copyright file="DuckTypeAotAdditionalParityTests.Calls.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// How the registry calls the target members dynamic duck typing binds: the arguments Type.GetMethod's default binder accepts
/// (boxing, enums and their underlying type), and the call instruction (a virtual call for public or generic methods, the
/// exact bound method otherwise).
/// </summary>
public partial class DuckTypeAotAdditionalParityTests
{
    [Theory]
    // Value types passed to object, interface and ValueType parameters.
    [InlineData("int-to-object", "object:Int32:1")]
    [InlineData("int-to-interface", "comparable:Int32:2")]
    [InlineData("int-to-value-type", "valuetype:Int32:3")]
    [InlineData("nullable-to-object", "object:Int32:4")]
    [InlineData("null-nullable-to-object", "object:null")]
    [InlineData("struct-to-object", "object:ArgStruct:S5")]
    [InlineData("date-time-to-interface", "formattable:2020")]
    [InlineData("two-boxed-arguments", "two:Int32:7,Int32:8")]
    [InlineData("static-method", "static-object:Int32:9")]
    [InlineData("private-method", "private-object:Int32:10")]
    [InlineData("struct-target", "struct-object:Int32:11")]
    // [Duck(ParameterTypeNames)] selecting an enum or object parameter for an int (the MongoDB BSON writer shape).
    [InlineData("parameter-type-names-int-to-enum", "[guid][guid:Standard]")]
    [InlineData("parameter-type-names-int-to-object", "value:Int32:12")]
    // By-ref enum parameters for by-ref ints.
    [InlineData("ref-int-to-ref-enum", "refenum|6")]
    [InlineData("out-int-to-out-enum", "outenum|0")]
    // Property setters and indexers.
    [InlineData("setter-int-to-object", "Int32:13")]
    [InlineData("setter-int-to-interface", "Int32:14")]
    [InlineData("setter-int-to-enum", "DayOfWeek:Tuesday")]
    [InlineData("setter-date-time-to-object", "DateTime:2000")]
    [InlineData("indexer-int-to-object", "object-indexer:Int32:15")]
    [InlineData("indexer-int-to-enum", "enum-indexer:Tuesday")]
    // A runtime type (alias) whose own method dynamic duck typing selects with boxing.
    [InlineData("derived-type-overload", "base-optional:16|derived-object:Int32:17")]
    // Omitted optional parameters of the target method: Nullable native integers.
    [InlineData("nullable-native-integer-defaults", "12")]
    // Generic methods with constraints (the proxy's generic parameters have them, so the target method can too), and proxy
    // methods with default parameter values.
    [InlineData("generic-method-constraints", "ArgNameHolder|ArgNameHolder|struct:4")]
    [InlineData("proxy-default-parameter-values", "3:x")]
    // Fallback method names, with a duck chained argument: the first name that has a method wins.
    [InlineData("fallback-names-in-order", "primary:chained")]
    // Controls: the binder selects the method, but dynamic duck typing can't convert the argument, or selects none.
    [InlineData("int-to-long", "throws:DuckTypeInvalidTypeConversionException")]
    [InlineData("int-to-enum-without-parameter-type-names", "throws:DuckTypeTargetMethodNotFoundException")]
    public void GeneratedRegistryShouldConvertArgumentsLikeDynamicMode(string scenario, string expected)
    {
        var target = new ArgTarget();
        var (exercise, mappings) = scenario switch
        {
            "int-to-object" => (Run(() => DuckType.Create<IArgObjectProxy>(target)!.TakeObject(1)), new[] { Mapping(typeof(IArgObjectProxy), typeof(ArgTarget)) }),
            "int-to-interface" => (Run(() => DuckType.Create<IArgComparableProxy>(target)!.TakeComparable(2)), new[] { Mapping(typeof(IArgComparableProxy), typeof(ArgTarget)) }),
            "int-to-value-type" => (Run(() => DuckType.Create<IArgValueTypeProxy>(target)!.TakeValueType(3)), new[] { Mapping(typeof(IArgValueTypeProxy), typeof(ArgTarget)) }),
            "nullable-to-object" => (Run(() => DuckType.Create<IArgNullableProxy>(target)!.TakeObject(4)), new[] { Mapping(typeof(IArgNullableProxy), typeof(ArgTarget)) }),
            "null-nullable-to-object" => (Run(() => DuckType.Create<IArgNullableProxy>(target)!.TakeObject(null)), new[] { Mapping(typeof(IArgNullableProxy), typeof(ArgTarget)) }),
            "struct-to-object" => (Run(() => DuckType.Create<IArgStructProxy>(target)!.TakeObject(new ArgStruct(5))), new[] { Mapping(typeof(IArgStructProxy), typeof(ArgTarget)) }),
            "date-time-to-interface" => (Run(() => DuckType.Create<IArgDateTimeProxy>(target)!.Format(new DateTime(2020, 1, 2))), new[] { Mapping(typeof(IArgDateTimeProxy), typeof(ArgTarget)) }),
            "two-boxed-arguments" => (Run(() => DuckType.Create<IArgTwoProxy>(target)!.TakeTwo(7, 8)), new[] { Mapping(typeof(IArgTwoProxy), typeof(ArgTarget)) }),
            "static-method" => (Run(() => DuckType.Create<IArgStaticProxy>(target)!.StaticTakeObject(9)), new[] { Mapping(typeof(IArgStaticProxy), typeof(ArgTarget)) }),
            "private-method" => (Run(() => DuckType.Create<IArgPrivateProxy>(target)!.PrivateTakeObject(10)), new[] { Mapping(typeof(IArgPrivateProxy), typeof(ArgTarget)) }),
            "struct-target" => (Run(() => DuckType.Create<IArgObjectProxy>(new ArgStructTarget())!.TakeObject(11)), new[] { Mapping(typeof(IArgObjectProxy), typeof(ArgStructTarget)) }),
            "parameter-type-names-int-to-enum" => (
                Run(() =>
                {
                    var writer = new ArgBsonWriter();
                    var proxy = DuckType.Create<IArgBsonWriterProxy>(writer)!;
                    proxy.WriteGuid(Guid.Empty);
                    proxy.WriteGuid(Guid.Empty, 4);
                    return writer.Log;
                }),
                new[] { Mapping(typeof(IArgBsonWriterProxy), typeof(ArgBsonWriter)) }),
            "parameter-type-names-int-to-object" => (
                Run(() =>
                {
                    var writer = new ArgBsonWriter();
                    DuckType.Create<IArgBsonWriterObjectProxy>(writer)!.WriteValue(12);
                    return writer.Log;
                }),
                new[] { Mapping(typeof(IArgBsonWriterObjectProxy), typeof(ArgBsonWriter)) }),
            "ref-int-to-ref-enum" => (
                Run(() =>
                {
                    var value = 1;
                    var result = DuckType.Create<IArgRefEnumProxy>(target)!.RefEnum(ref value);
                    return result + "|" + value;
                }),
                new[] { Mapping(typeof(IArgRefEnumProxy), typeof(ArgTarget)) }),
            "out-int-to-out-enum" => (
                Run(() =>
                {
                    var result = DuckType.Create<IArgOutEnumProxy>(target)!.OutEnum(out var value);
                    return result + "|" + value;
                }),
                new[] { Mapping(typeof(IArgOutEnumProxy), typeof(ArgTarget)) }),
            "setter-int-to-object" => (
                Run(() =>
                {
                    DuckType.Create<IArgObjectSetterProxy>(target)!.ObjectProp = 13;
                    return DescribeArgument(target.ObjectProp);
                }),
                new[] { Mapping(typeof(IArgObjectSetterProxy), typeof(ArgTarget)) }),
            "setter-int-to-interface" => (
                Run(() =>
                {
                    DuckType.Create<IArgComparableSetterProxy>(target)!.ComparableProp = 14;
                    return DescribeArgument(target.ComparableProp);
                }),
                new[] { Mapping(typeof(IArgComparableSetterProxy), typeof(ArgTarget)) }),
            "setter-int-to-enum" => (
                Run(() =>
                {
                    DuckType.Create<IArgEnumSetterProxy>(target)!.EnumProp = 2;
                    return DescribeArgument(target.EnumProp);
                }),
                new[] { Mapping(typeof(IArgEnumSetterProxy), typeof(ArgTarget)) }),
            "setter-date-time-to-object" => (
                Run(() =>
                {
                    DuckType.Create<IArgDateTimeSetterProxy>(target)!.ObjectProp = new DateTime(2000, 1, 1);
                    return target.ObjectProp?.GetType().Name + ":" + ((DateTime)target.ObjectProp!).Year;
                }),
                new[] { Mapping(typeof(IArgDateTimeSetterProxy), typeof(ArgTarget)) }),
            "indexer-int-to-object" => (Run(() => DuckType.Create<IArgIndexerProxy>(new ArgObjectIndexerTarget())![15]), new[] { Mapping(typeof(IArgIndexerProxy), typeof(ArgObjectIndexerTarget)) }),
            "indexer-int-to-enum" => (Run(() => DuckType.Create<IArgIndexerProxy>(new ArgEnumIndexerTarget())![2]), new[] { Mapping(typeof(IArgIndexerProxy), typeof(ArgEnumIndexerTarget)) }),
            "derived-type-overload" => (
                Run(() => DuckType.Create<IArgShowProxy>(new ArgOptionalShowBase())!.Show(16) + "|" + DuckType.Create<IArgShowProxy>(new ArgObjectShowDerived())!.Show(17)),
                new[] { Mapping(typeof(IArgShowProxy), typeof(ArgOptionalShowBase)) }),
            "generic-method-constraints" => (
                Run(() =>
                    DuckType.Create<IArgConstrainedFactoryProxy>(new ArgConstrainedFactoryTarget())!.Create<ArgNameHolder>().GetType().Name + "|" +
                    ((IArgConstrainedFactoryProxy)DuckType.CreateReverse(typeof(IArgConstrainedFactoryProxy), new ArgConstrainedFactory())).Create<ArgNameHolder>().GetType().Name + "|" +
                    ((IArgStructDescriberProxy)DuckType.CreateReverse(typeof(IArgStructDescriberProxy), new ArgStructDescriber())).Describe(4)),
                new[]
                {
                    Mapping(typeof(IArgConstrainedFactoryProxy), typeof(ArgConstrainedFactoryTarget)),
                    Mapping(typeof(IArgConstrainedFactoryProxy), typeof(ArgConstrainedFactory), reverse: true),
                    Mapping(typeof(IArgStructDescriberProxy), typeof(ArgStructDescriber), reverse: true),
                }),
            "proxy-default-parameter-values" => (Run(() => DuckType.Create<IArgDefaultValuesProxy>(new ArgDefaultValuesTarget())!.Format(3)), new[] { Mapping(typeof(IArgDefaultValuesProxy), typeof(ArgDefaultValuesTarget)) }),
            "fallback-names-in-order" => (
                Run(() => DuckType.Create<IArgFallbackDescribeProxy>(new ArgFallbackDescribeTarget())!.Describe(DuckType.Create<ICallNameView>(new CallChainValue())!)),
                new[] { Mapping(typeof(IArgFallbackDescribeProxy), typeof(ArgFallbackDescribeTarget)), Mapping(typeof(ICallNameView), typeof(CallChainValue)) }),
            "nullable-native-integer-defaults" => (Run(() => DuckType.Create<IArgShowProxy>(new ArgNullableNativeIntegerTarget())!.Show(1)), new[] { Mapping(typeof(IArgShowProxy), typeof(ArgNullableNativeIntegerTarget)) }),
            "int-to-long" => (Run(() => DuckType.Create<IArgLongProxy>(target)!.TakeLong(18)), new[] { Mapping(typeof(IArgLongProxy), typeof(ArgTarget)) }),
            _ => (Run(() => DuckType.Create<IArgEnumProxy>(target)!.TakeEnum(19)), new[] { Mapping(typeof(IArgEnumProxy), typeof(ArgTarget)) }),
        };

        AssertSameOutcome(expected, exercise, mappings);
    }

    [Theory]
    // Contract parameters implemented with a value type: unboxed, or converted to the enum's underlying type.
    [InlineData("object-from-int", "int:3")]
    [InlineData("interface-from-int", "int:6")]
    [InlineData("object-from-struct", "struct:9")]
    [InlineData("property-object-from-int", "Int32:4|4")]
    [InlineData("property-enum-from-int", "Saturday|6")]
    [InlineData("ref-enum-from-ref-int", "swapped|Saturday")]
    // [DuckReverseMethod(ParameterTypeNames)] (the ILogger and MongoDB BSON writer shapes).
    [InlineData("parameter-type-names-enum-from-int", "True")]
    [InlineData("parameter-type-names-generic-method", "log:1:fmt:state")]
    [InlineData("parameter-type-names-object-from-int", "int:9")]
    [InlineData("parameter-type-names-bson-writer", "[rev-guid][rev-guid:4]")]
    // Control: without ParameterTypeNames, dynamic duck typing pairs no method with an enum parameter implemented with an int.
    [InlineData("enum-from-int-without-parameter-type-names", "throws:TargetInvocationException>DuckTypeTargetMethodNotFoundException")]
    public void GeneratedRegistryShouldConvertReverseArgumentsLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mappings) = scenario switch
        {
            "object-from-int" => (
                Reverse<IArgRevObjectContract>(new ArgRevIntImpl(), r => r.Take(3)),
                new[] { Mapping(typeof(IArgRevObjectContract), typeof(ArgRevIntImpl), reverse: true) }),
            "interface-from-int" => (
                Reverse<IArgRevComparableContract>(new ArgRevIntImpl(), r => r.Take(6)),
                new[] { Mapping(typeof(IArgRevComparableContract), typeof(ArgRevIntImpl), reverse: true) }),
            "object-from-struct" => (
                Reverse<IArgRevObjectContract>(new ArgRevStructImpl(), r => r.Take(new ArgStruct(9))),
                new[] { Mapping(typeof(IArgRevObjectContract), typeof(ArgRevStructImpl), reverse: true) }),
            "property-object-from-int" => (
                Run(() =>
                {
                    var impl = new ArgRevIntPropertyImpl();
                    var proxy = (IArgRevObjectPropertyContract)DuckType.CreateReverse(typeof(IArgRevObjectPropertyContract), impl);
                    proxy.Value = 4;
                    return DescribeArgument(proxy.Value) + "|" + impl.Value;
                }),
                new[] { Mapping(typeof(IArgRevObjectPropertyContract), typeof(ArgRevIntPropertyImpl), reverse: true) }),
            "property-enum-from-int" => (
                Run(() =>
                {
                    var impl = new ArgRevIntLevelImpl();
                    var proxy = (IArgRevEnumPropertyContract)DuckType.CreateReverse(typeof(IArgRevEnumPropertyContract), impl);
                    proxy.Level = DayOfWeek.Saturday;
                    return proxy.Level + "|" + impl.Level;
                }),
                new[] { Mapping(typeof(IArgRevEnumPropertyContract), typeof(ArgRevIntLevelImpl), reverse: true) }),
            "ref-enum-from-ref-int" => (
                Run(() =>
                {
                    var value = DayOfWeek.Monday;
                    var result = ((IArgRevRefEnumContract)DuckType.CreateReverse(typeof(IArgRevRefEnumContract), new ArgRevRefIntImpl())).Swap(ref value);
                    return result + "|" + value;
                }),
                new[] { Mapping(typeof(IArgRevRefEnumContract), typeof(ArgRevRefIntImpl), reverse: true) }),
            "parameter-type-names-enum-from-int" => (
                Reverse<IArgRevLoggerContract>(new ArgRevLoggerImpl(), r => r.IsEnabled(DayOfWeek.Wednesday)),
                new[] { Mapping(typeof(IArgRevLoggerContract), typeof(ArgRevLoggerImpl), reverse: true) }),
            "parameter-type-names-generic-method" => (
                Reverse<IArgRevLoggerContract>(new ArgRevLoggerImpl(), r => r.Log(DayOfWeek.Monday, "state", null, (s, e) => "fmt:" + s)),
                new[] { Mapping(typeof(IArgRevLoggerContract), typeof(ArgRevLoggerImpl), reverse: true) }),
            "parameter-type-names-object-from-int" => (
                Reverse<IArgRevObjectContract>(new ArgRevNamedIntImpl(), r => r.Take(9)),
                new[] { Mapping(typeof(IArgRevObjectContract), typeof(ArgRevNamedIntImpl), reverse: true) }),
            "parameter-type-names-bson-writer" => (
                Run(() =>
                {
                    var impl = new ArgRevBsonWriter();
                    var writer = (IArgBsonWriterContract)DuckType.CreateReverse(typeof(IArgBsonWriterContract), impl);
                    writer.WriteGuid(Guid.Empty);
                    writer.WriteGuid(Guid.Empty, ArgGuidRepresentation.Standard);
                    return impl.Log;
                }),
                new[] { Mapping(typeof(IArgBsonWriterContract), typeof(ArgRevBsonWriter), reverse: true) }),
            _ => (
                Reverse<IArgRevEnumContract>(new ArgRevIntImpl(), r => r.Take(DayOfWeek.Friday)),
                new[] { Mapping(typeof(IArgRevEnumContract), typeof(ArgRevIntImpl), reverse: true) }),
        };

        AssertSameOutcome(expected, exercise, mappings);
    }

    [Theory]
    // Non-public, non-generic members are called without a null check (dynamic duck typing calls them through a function pointer).
    [InlineData("null-instance-internal-method", "const")]
    [InlineData("null-instance-private-property", "secret")]
    [InlineData("null-instance-protected-virtual-method", "kind")]
    [InlineData("null-instance-explicit-interface-property", "explicit-const")]
    [InlineData("null-instance-duck-copy-private-property", "secret")]
    [InlineData("null-reverse-delegation-internal-method", "rconst")]
    // Public or generic members are called with a null check.
    [InlineData("null-instance-public-method", "throws:NullReferenceException")]
    [InlineData("null-instance-internal-generic-method", "throws:NullReferenceException")]
    [InlineData("null-reverse-delegation-public-method", "throws:NullReferenceException")]
    // A proxy type created for a base type over a derived instance: non-public virtual members run the bound (base) member,
    // public and generic ones dispatch virtually.
    [InlineData("derived-instance-protected-virtual-method", "base|pub-derived")]
    [InlineData("derived-instance-protected-virtual-property", "base-prop")]
    [InlineData("derived-instance-protected-virtual-property-class-proxy", "base-prop")]
    [InlineData("derived-instance-duck-copy-protected-virtual-property", "base-prop")]
    [InlineData("derived-instance-public-virtual-property", "pub-derived-prop")]
    [InlineData("derived-instance-internal-generic-methods", "gen-derived|gen-nonvirtual")]
    // Value-type targets.
    [InlineData("struct-target-internal-method", "struct:5")]
    public void GeneratedRegistryShouldCallTargetMembersLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mappings) = scenario switch
        {
            "null-instance-internal-method" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallConstantView), typeof(CallNullTarget)).CreateInstance<ICallConstantView>(null!).Constant()),
                new[] { Mapping(typeof(ICallConstantView), typeof(CallNullTarget)) }),
            "null-instance-private-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallSecretView), typeof(CallNullTarget)).CreateInstance<ICallSecretView>(null!).Secret),
                new[] { Mapping(typeof(ICallSecretView), typeof(CallNullTarget)) }),
            "null-instance-protected-virtual-method" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallKindView), typeof(CallNullTarget)).CreateInstance<ICallKindView>(null!).Kind()),
                new[] { Mapping(typeof(ICallKindView), typeof(CallNullTarget)) }),
            "null-instance-explicit-interface-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallExplicitView), typeof(CallExplicitTarget)).CreateInstance<ICallExplicitView>(null!).Name),
                new[] { Mapping(typeof(ICallExplicitView), typeof(CallExplicitTarget)) }),
            "null-instance-duck-copy-private-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(CallSecretCopy), typeof(CallNullTarget)).CreateInstance<CallSecretCopy>(null!).Secret),
                new[] { Mapping(typeof(CallSecretCopy), typeof(CallNullTarget)) }),
            "null-reverse-delegation-internal-method" => (
                Run(() => DuckType.GetOrCreateReverseProxyType(typeof(ICallConstantContract), typeof(CallConstantDelegation)).CreateInstance<ICallConstantContract>(null!).Constant()),
                new[] { Mapping(typeof(ICallConstantContract), typeof(CallConstantDelegation), reverse: true) }),
            "null-instance-public-method" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallPublicView), typeof(CallNullTarget)).CreateInstance<ICallPublicView>(null!).Pub()),
                new[] { Mapping(typeof(ICallPublicView), typeof(CallNullTarget)) }),
            "null-instance-internal-generic-method" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallGenericView), typeof(CallGenericBase)).CreateInstance<ICallGenericView>(null!).GenNonVirtual(1)),
                new[] { Mapping(typeof(ICallGenericView), typeof(CallGenericBase)) }),
            "null-reverse-delegation-public-method" => (
                Run(() => DuckType.GetOrCreateReverseProxyType(typeof(ICallConstantContract), typeof(CallPublicConstantDelegation)).CreateInstance<ICallConstantContract>(null!).Constant()),
                new[] { Mapping(typeof(ICallConstantContract), typeof(CallPublicConstantDelegation), reverse: true) }),
            "derived-instance-protected-virtual-method" => (
                Run(() =>
                {
                    var proxy = DuckType.GetOrCreateProxyType(typeof(ICallKindView), typeof(CallKindBase)).CreateInstance<ICallKindView>(new CallKindDerived());
                    return proxy.Kind() + "|" + proxy.PubKind();
                }),
                new[] { Mapping(typeof(ICallKindView), typeof(CallKindBase)) }),
            "derived-instance-protected-virtual-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallKindPropertyView), typeof(CallKindPropertyBase)).CreateInstance<ICallKindPropertyView>(new CallKindPropertyDerived()).Kind),
                new[] { Mapping(typeof(ICallKindPropertyView), typeof(CallKindPropertyBase)) }),
            "derived-instance-protected-virtual-property-class-proxy" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallKindPropertyClassView), typeof(CallKindPropertyBase)).CreateInstance<ICallKindPropertyClassView>(new CallKindPropertyDerived()).Kind),
                new[] { Mapping(typeof(ICallKindPropertyClassView), typeof(CallKindPropertyBase)) }),
            "derived-instance-duck-copy-protected-virtual-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(CallKindCopy), typeof(CallKindPropertyBase)).CreateInstance<CallKindCopy>(new CallKindPropertyDerived()).Kind),
                new[] { Mapping(typeof(CallKindCopy), typeof(CallKindPropertyBase)) }),
            "derived-instance-public-virtual-property" => (
                Run(() => DuckType.GetOrCreateProxyType(typeof(ICallKindPropertyView), typeof(CallPublicKindPropertyBase)).CreateInstance<ICallKindPropertyView>(new CallPublicKindPropertyDerived()).Kind),
                new[] { Mapping(typeof(ICallKindPropertyView), typeof(CallPublicKindPropertyBase)) }),
            "derived-instance-internal-generic-methods" => (
                Run(() =>
                {
                    var proxy = DuckType.GetOrCreateProxyType(typeof(ICallGenericView), typeof(CallGenericBase)).CreateInstance<ICallGenericView>(new CallGenericDerived());
                    return proxy.Gen(1) + "|" + proxy.GenNonVirtual(1);
                }),
                new[] { Mapping(typeof(ICallGenericView), typeof(CallGenericBase)) }),
            _ => (
                Run(() => DuckType.Create<ICallConstantView>(new CallConstantStruct(5))!.Constant()),
                new[] { Mapping(typeof(ICallConstantView), typeof(CallConstantStruct)) }),
        };

        AssertSameOutcome(expected, exercise, mappings);
    }

    [Theory]
    // [DuckInclude] methods whose signature uses a generic parameter of the type declaring them: closed like the target's members.
    [InlineData("generic-base-class", "genbase:7")]
    [InlineData("generic-target", "s")]
    [InlineData("generic-base-class-override", "derived:s")]
    public void GeneratedRegistryShouldIncludeMethodsOfGenericTargetsLikeDynamicMode(string scenario, string expected)
    {
        var (exercise, mapping) = scenario switch
        {
            "generic-base-class" => (Run(() => InvokeIncluded(DuckType.Create<ICallEmptyProxy>(new CallIncludeGenericDerived())!, "Describe", 7)), Mapping(typeof(ICallEmptyProxy), typeof(CallIncludeGenericDerived))),
            "generic-target" => (Run(() => InvokeIncluded(DuckType.Create<ICallEmptyProxy>(new CallIncludeGenericTarget<string>())!, "Get", "s")), Mapping(typeof(ICallEmptyProxy), typeof(CallIncludeGenericTarget<string>))),
            _ => (Run(() => InvokeIncluded(DuckType.Create<ICallEmptyProxy>(new CallIncludeGenericOverride())!, "Describe", "s")), Mapping(typeof(ICallEmptyProxy), typeof(CallIncludeGenericOverride))),
        };

        AssertSameOutcome(expected, exercise, mapping);

        static object? InvokeIncluded(object proxy, string name, object argument)
            => proxy.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single(method => method.Name == name).Invoke(proxy, [argument]);
    }

    [Fact]
    public void GeneratedRegistryShouldTypeReversePropertiesWithTheTypeOfThePropertyTheyImplement()
    {
        // The delegation's property is a proxy of the contract property's type: the reverse proxy's property has the type of the
        // contract's (its getter's), which a forward view over the reverse proxy duck chains.
        AssertSameOutcome(
            "CallChainValue|chained",
            () =>
            {
                var reverse = DuckType.CreateReverse(typeof(CallChainContract), new CallChainDelegation());
                return reverse.GetType().GetProperty(nameof(CallChainContract.Value))!.PropertyType.Name + "|" + DuckType.Create<ICallChainView>(reverse)!.Value!.Name;
            },
            Mapping(typeof(CallChainContract), typeof(CallChainDelegation), reverse: true),
            Mapping(typeof(ICallNameView), typeof(CallChainValue)),
            Mapping(typeof(ICallChainView), typeof(CallChainContract)));
    }

    [Fact]
    public void GeneratedRegistryShouldReplayEveryExceptionDynamicDuckTypingWraps()
    {
        // The proxy's generic parameter has no constraint, the target's has one: dynamic duck typing fails with a
        // DuckTypeException wrapping an ArgumentException wrapping a VerificationException, which the registry replays.
        Func<object?> exercise = () => DuckType.Create<IArgUnconstrainedDescribeProxy>(new ArgStructDescribeTarget())!.Describe(3);
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureExceptionChain(exercise);
        expected.Should().StartWith("DuckTypeException:").And.Contain(" > ArgumentException:").And.Contain(" > VerificationException:");
        WithGeneratedRegistry(() => CaptureExceptionChain(exercise).Should().Be(expected), Mapping(typeof(IArgUnconstrainedDescribeProxy), typeof(ArgStructDescribeTarget)));

        static string CaptureExceptionChain(Func<object?> exercise)
        {
            try
            {
                return "value:" + exercise();
            }
            catch (Exception ex)
            {
                var chain = new List<string>();
                for (Exception? current = ex; current is not null; current = current.InnerException)
                {
                    chain.Add(current.GetType().Name + ":" + current.Message);
                }

                return string.Join(" > ", chain);
            }
        }
    }

    [Fact]
    public void GeneratedRegistryShouldReplayTheFailureOfArrayProxyTypes()
    {
        // Duck chaining an array member creates a proxy of the proxy's array type, which dynamic duck typing can't create: the
        // mapping recorded for it (an array proxy type) replays that failure.
        Func<object?> exercise = () => string.Join(",", DuckType.Create<ICallChildrenProxy>(new CallChildrenTarget())!.Children.Select(child => child.Name));
        DuckType.ResetRuntimeModeForTests();
        var expected = CaptureFailure(exercise);
        expected.Should().StartWith("throws:DuckTypePropertyOrFieldNotFoundException:");
        WithGeneratedRegistry(
            () => CaptureFailure(exercise).Should().Be(expected),
            matrix => matrix.Mappings.Single(mapping => mapping.ProxyType!.EndsWith("[]", StringComparison.Ordinal)).DynamicFailureReplayed.Should().BeTrue(),
            Mapping(typeof(ICallChildrenProxy), typeof(CallChildrenTarget)),
            Mapping(typeof(ICallNameView[]), typeof(CallChainValue[])),
            Mapping(typeof(ICallNameView), typeof(CallChainValue)));
    }

    private static Func<object?> Run(Func<object?> exercise) => exercise;

    private static string DescribeArgument(object? value)
        => value is null ? "null" : value.GetType().Name + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);

    public enum ArgGuidRepresentation
    {
        Unspecified = 0,
        CSharpLegacy = 3,
        Standard = 4,
    }

    public readonly struct ArgStruct
    {
        public ArgStruct(int value) => Value = value;

        public int Value { get; }

        public override string ToString() => "S" + Value.ToString(CultureInfo.InvariantCulture);
    }

    public class ArgTarget
    {
        public object? ObjectProp { get; set; }

        public IComparable? ComparableProp { get; set; }

        public DayOfWeek EnumProp { get; set; }

        public static string StaticTakeObject(object value) => "static-object:" + DescribeArgument(value);

        public string TakeObject(object? value) => "object:" + DescribeArgument(value);

        public string TakeComparable(IComparable value) => "comparable:" + DescribeArgument(value);

        public string TakeValueType(ValueType value) => "valuetype:" + DescribeArgument(value);

        public string TakeEnum(DayOfWeek value) => "enum:" + value;

        public string TakeLong(long value) => "long:" + value.ToString(CultureInfo.InvariantCulture);

        public string TakeTwo(object first, object second) => "two:" + DescribeArgument(first) + "," + DescribeArgument(second);

        public string Format(IFormattable value) => "formattable:" + value.ToString("yyyy", CultureInfo.InvariantCulture);

        public string RefEnum(ref DayOfWeek value)
        {
            value = DayOfWeek.Saturday;
            return "refenum";
        }

        public string OutEnum(out DayOfWeek value)
        {
            value = DayOfWeek.Sunday;
            return "outenum";
        }

        private string PrivateTakeObject(object value) => "private-object:" + DescribeArgument(value);
    }

    public struct ArgStructTarget
    {
        public string TakeObject(object value) => "struct-object:" + DescribeArgument(value);
    }

    public class ArgObjectIndexerTarget
    {
        public string this[object key] => "object-indexer:" + DescribeArgument(key);
    }

    public class ArgEnumIndexerTarget
    {
        public string this[DayOfWeek key] => "enum-indexer:" + key;
    }

    public interface IArgConstrainedFactoryProxy
    {
        T Create<T>()
            where T : class, new();
    }

    public interface IArgStructDescriberProxy
    {
        string Describe<T>(T value)
            where T : struct;
    }

    public interface IArgUnconstrainedDescribeProxy
    {
        string Describe<T>(T value);
    }

    public class ArgStructDescribeTarget
    {
        public string Describe<T>(T value)
            where T : struct
            => "struct:" + value;
    }

    public interface IArgDefaultValuesProxy
    {
        string Format(int value, string text = "x");
    }

    public class ArgNameHolder
    {
    }

    public class ArgConstrainedFactoryTarget
    {
        public T Create<T>()
            where T : class, new()
            => new T();
    }

    public class ArgConstrainedFactory
    {
        [DuckReverseMethod]
        public T Create<T>()
            where T : class, new()
            => new T();
    }

    public class ArgStructDescriber
    {
        [DuckReverseMethod]
        public string Describe<T>(T value)
            where T : struct
            => "struct:" + value;
    }

    public class ArgDefaultValuesTarget
    {
        public string Format(int value, string text) => value.ToString(CultureInfo.InvariantCulture) + ":" + text;
    }

    public interface IArgFallbackDescribeProxy
    {
        [Duck(Name = "Primary,Secondary")]
        string Describe(ICallNameView value);
    }

    public class ArgFallbackDescribeTarget
    {
        public string Secondary(CallChainValue value) => "secondary:" + value.Name;

        public string Primary(CallChainValue value) => "primary:" + value.Name;
    }

    public class ArgNullableNativeIntegerTarget
    {
        public string Show(int value, nint? offset = 5, nuint? extra = 6) => (value + (int)offset!.Value + (int)extra!.Value).ToString(CultureInfo.InvariantCulture);
    }

    public class ArgOptionalShowBase
    {
        public string Show(int value, int extra = 0) => "base-optional:" + value.ToString(CultureInfo.InvariantCulture);
    }

    public class ArgObjectShowDerived : ArgOptionalShowBase
    {
        public string Show(object value) => "derived-object:" + DescribeArgument(value);
    }

    public class ArgBsonWriter
    {
        public string Log { get; private set; } = string.Empty;

        public void WriteGuid(Guid guid) => Log += "[guid]";

        public void WriteGuid(Guid guid, ArgGuidRepresentation representation) => Log += "[guid:" + representation + "]";

        public void WriteValue(object value) => Log += "value:" + DescribeArgument(value);
    }

    public interface IArgObjectProxy
    {
        string TakeObject(int value);
    }

    public interface IArgComparableProxy
    {
        string TakeComparable(int value);
    }

    public interface IArgValueTypeProxy
    {
        string TakeValueType(int value);
    }

    public interface IArgNullableProxy
    {
        string TakeObject(int? value);
    }

    public interface IArgStructProxy
    {
        string TakeObject(ArgStruct value);
    }

    public interface IArgDateTimeProxy
    {
        string Format(DateTime value);
    }

    public interface IArgTwoProxy
    {
        string TakeTwo(int first, int second);
    }

    public interface IArgStaticProxy
    {
        string StaticTakeObject(int value);
    }

    public interface IArgPrivateProxy
    {
        string PrivateTakeObject(int value);
    }

    public interface IArgEnumProxy
    {
        string TakeEnum(int value);
    }

    public interface IArgLongProxy
    {
        string TakeLong(int value);
    }

    public interface IArgRefEnumProxy
    {
        string RefEnum(ref int value);
    }

    public interface IArgOutEnumProxy
    {
        string OutEnum(out int value);
    }

    public interface IArgObjectSetterProxy
    {
        int ObjectProp { set; }
    }

    public interface IArgComparableSetterProxy
    {
        int ComparableProp { set; }
    }

    public interface IArgEnumSetterProxy
    {
        int EnumProp { set; }
    }

    public interface IArgDateTimeSetterProxy
    {
        DateTime ObjectProp { set; }
    }

    public interface IArgIndexerProxy
    {
        string this[int key] { get; }
    }

    public interface IArgShowProxy
    {
        string Show(int value);
    }

    public interface IArgBsonWriterProxy
    {
        void WriteGuid(Guid guid);

        [Duck(ParameterTypeNames = ["System.Guid", "Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+ArgGuidRepresentation, Datadog.Trace.Tools.Runner.Tests"])]
        void WriteGuid(Guid guid, int guidRepresentation);
    }

    public interface IArgBsonWriterObjectProxy
    {
        [Duck(ParameterTypeNames = ["System.Object"])]
        void WriteValue(int value);
    }

    public interface IArgBsonWriterContract
    {
        void WriteGuid(Guid guid);

        void WriteGuid(Guid guid, ArgGuidRepresentation guidRepresentation);
    }

    public interface IArgRevObjectContract
    {
        string Take(object value);
    }

    public interface IArgRevComparableContract
    {
        string Take(IComparable value);
    }

    public interface IArgRevEnumContract
    {
        string Take(DayOfWeek value);
    }

    public interface IArgRevObjectPropertyContract
    {
        object Value { get; set; }
    }

    public interface IArgRevEnumPropertyContract
    {
        DayOfWeek Level { get; set; }
    }

    public interface IArgRevRefEnumContract
    {
        string Swap(ref DayOfWeek value);
    }

    public interface IArgRevLoggerContract
    {
        bool IsEnabled(DayOfWeek level);

        string Log<TState>(DayOfWeek level, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    }

    public class ArgRevIntImpl
    {
        [DuckReverseMethod]
        public string Take(int value) => "int:" + value.ToString(CultureInfo.InvariantCulture);
    }

    public class ArgRevNamedIntImpl
    {
        [DuckReverseMethod(ParameterTypeNames = ["System.Object"])]
        public string Take(int value) => "int:" + value.ToString(CultureInfo.InvariantCulture);
    }

    public class ArgRevStructImpl
    {
        [DuckReverseMethod]
        public string Take(ArgStruct value) => "struct:" + value.Value.ToString(CultureInfo.InvariantCulture);
    }

    public class ArgRevIntPropertyImpl
    {
        [DuckReverseMethod]
        public int Value { get; set; }
    }

    public class ArgRevIntLevelImpl
    {
        [DuckReverseMethod]
        public int Level { get; set; }
    }

    public class ArgRevRefIntImpl
    {
        [DuckReverseMethod]
        public string Swap(ref int value)
        {
            value = 6;
            return "swapped";
        }
    }

    public class ArgRevLoggerImpl
    {
        [DuckReverseMethod(ParameterTypeNames = ["System.DayOfWeek"])]
        public bool IsEnabled(int level) => level >= 3;

        [DuckReverseMethod(ParameterTypeNames = ["System.DayOfWeek", "TState", "System.Exception", "Func`3"])]
        public string Log<TState>(int level, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => "log:" + level.ToString(CultureInfo.InvariantCulture) + ":" + formatter(state, exception);
    }

    public class ArgRevBsonWriter
    {
        public string Log { get; private set; } = string.Empty;

        [DuckReverseMethod]
        public void WriteGuid(Guid guid) => Log += "[rev-guid]";

        [DuckReverseMethod(ParameterTypeNames = ["System.Guid", "Datadog.Trace.Tools.Runner.Tests.DuckTypeAotAdditionalParityTests+ArgGuidRepresentation, Datadog.Trace.Tools.Runner.Tests"])]
        public void WriteGuid(Guid guid, int guidRepresentation) => Log += "[rev-guid:" + guidRepresentation.ToString(CultureInfo.InvariantCulture) + "]";
    }

    public class CallNullTarget
    {
        private string Secret => "secret";

        public string Pub() => "pub";

        public virtual string PubKind() => "pubkind";

        internal string Constant() => "const";

        protected virtual string Kind() => "kind";
    }

    public class CallKindBase
    {
        public virtual string PubKind() => "pub-base";

        protected virtual string Kind() => "base";
    }

    public class CallKindDerived : CallKindBase
    {
        public override string PubKind() => "pub-derived";

        protected override string Kind() => "derived";
    }

    public class CallKindPropertyBase
    {
        protected virtual string Kind => "base-prop";
    }

    public class CallKindPropertyDerived : CallKindPropertyBase
    {
        protected override string Kind => "derived-prop";
    }

    public class CallPublicKindPropertyBase
    {
        public virtual string Kind => "pub-base-prop";
    }

    public class CallPublicKindPropertyDerived : CallPublicKindPropertyBase
    {
        public override string Kind => "pub-derived-prop";
    }

    public class CallGenericBase
    {
        internal virtual string Gen<T>(T value) => "gen-base";

        internal string GenNonVirtual<T>(T value) => "gen-nonvirtual";
    }

    public class CallGenericDerived : CallGenericBase
    {
        internal override string Gen<T>(T value) => "gen-derived";
    }

    public class CallExplicitTarget : ICallNameView
    {
        string ICallNameView.Name => "explicit-const";
    }

    public struct CallConstantStruct
    {
        private readonly int _value;

        public CallConstantStruct(int value) => _value = value;

        internal string Constant() => "struct:" + _value.ToString(CultureInfo.InvariantCulture);
    }

    public class CallConstantDelegation
    {
        [DuckReverseMethod]
        internal string Constant() => "rconst";
    }

    public class CallPublicConstantDelegation
    {
        [DuckReverseMethod]
        public string Constant() => "rpubconst";
    }

    [DuckCopy]
    public struct CallSecretCopy
    {
        public string Secret;
    }

    [DuckCopy]
    public struct CallKindCopy
    {
        public string Kind;
    }

    public class CallIncludeGenericBase<T>
    {
        [DuckInclude]
        public virtual string Describe(T value) => "genbase:" + value;
    }

    public class CallIncludeGenericDerived : CallIncludeGenericBase<int>
    {
    }

    public class CallIncludeGenericTarget<T>
    {
        [DuckInclude]
        public virtual T Get(T value) => value;
    }

    public class CallIncludeGenericOverride : CallIncludeGenericBase<string>
    {
        public override string Describe(string value) => "derived:" + value;
    }

    public interface ICallEmptyProxy
    {
    }

    public interface ICallChildrenProxy
    {
        ICallNameView[] Children { get; }
    }

    public class CallChildrenTarget
    {
        public CallChainValue[] Children => [new CallChainValue()];
    }

    public interface ICallChainView
    {
        ICallNameView? Value { get; }
    }

    public abstract class CallChainContract
    {
        public abstract CallChainValue? Value { get; }
    }

    public class CallChainValue
    {
        public string Name => "chained";
    }

    public class CallChainDelegation
    {
        [DuckReverseMethod]
        public ICallNameView? Value => DuckType.Create<ICallNameView>(new CallChainValue());
    }

    public interface ICallConstantView
    {
        string Constant();
    }

    public interface ICallConstantContract
    {
        string Constant();
    }

    public interface ICallSecretView
    {
        string Secret { get; }
    }

    public interface ICallPublicView
    {
        string Pub();
    }

    public interface ICallKindView
    {
        string Kind();

        string PubKind();
    }

    public interface ICallKindPropertyView
    {
        string Kind { get; }
    }

    [DuckAsClass]
    public interface ICallKindPropertyClassView
    {
        string Kind { get; }
    }

    public interface ICallGenericView
    {
        string Gen<T>(T value);

        string GenNonVirtual<T>(T value);
    }

    public interface ICallNameView
    {
        string Name { get; }
    }

    public interface ICallExplicitView
    {
        [Duck(ExplicitInterfaceTypeName = "*")]
        string Name { get; }
    }
}
