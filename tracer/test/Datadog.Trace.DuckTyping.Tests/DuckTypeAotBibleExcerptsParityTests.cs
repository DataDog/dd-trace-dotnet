// <copyright file="DuckTypeAotBibleExcerptsParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1401 // Fields should be private

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotBibleExcerptsParityTests
    {
        private static readonly MethodInfo? DynamicForwardFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicProxyType", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? DynamicReverseFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicReverseProxyType", BindingFlags.NonPublic | BindingFlags.Static);

        public DuckTypeAotBibleExcerptsParityTests()
        {
            DuckTypeAotEngine.ResetForTests();
        }

        [Fact]
        public void DifferentialParityTXAProxyShapeDefaultAndDuckAsClassShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-A";

            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxADefaultProxy),
                typeof(TxATarget),
                typeof(TxADefaultAotProxy),
                instance => new TxADefaultAotProxy((TxATarget)instance!));

            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxAClassProxy),
                typeof(TxATarget),
                typeof(TxAClassAotProxy),
                instance => new TxAClassAotProxy((TxATarget)instance!));

            var dynamicDefaultResult = InvokeDynamicForward(typeof(ITxADefaultProxy), typeof(TxATarget));
            var aotDefaultResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxADefaultProxy), typeof(TxATarget));
            var dynamicClassResult = InvokeDynamicForward(typeof(ITxAClassProxy), typeof(TxATarget));
            var aotClassResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxAClassProxy), typeof(TxATarget));

            AssertCanCreate(scenarioId, dynamicDefaultResult, aotDefaultResult);
            AssertCanCreate(scenarioId, dynamicClassResult, aotClassResult);

            dynamicDefaultResult.ProxyType!.IsValueType.Should().BeTrue($"scenario {scenarioId} dynamic default proxy should be a value type");
            aotDefaultResult.ProxyType!.IsValueType.Should().Be(dynamicDefaultResult.ProxyType!.IsValueType, $"scenario {scenarioId} default proxy shape should match");
            dynamicClassResult.ProxyType!.IsClass.Should().BeTrue($"scenario {scenarioId} dynamic DuckAsClass proxy should be a class");
            aotClassResult.ProxyType!.IsClass.Should().Be(dynamicClassResult.ProxyType!.IsClass, $"scenario {scenarioId} class proxy shape should match");

            var target = new TxATarget();
            dynamicDefaultResult.CreateInstance<ITxADefaultProxy>(target).SayHi().Should().Be("Hello World");
            aotDefaultResult.CreateInstance<ITxADefaultProxy>(target).SayHi().Should().Be("Hello World");
            dynamicClassResult.CreateInstance<ITxAClassProxy>(target).SayHi().Should().Be("Hello World");
            aotClassResult.CreateInstance<ITxAClassProxy>(target).SayHi().Should().Be("Hello World");
        }

        [Fact]
        public void DifferentialParityTXBPropertyOrFieldFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-B";
            DuckTypeAotEngine.RegisterProxy(
                typeof(TxBProxy),
                typeof(TxBTarget),
                typeof(TxBProxy),
                instance =>
                {
                    var target = (TxBTarget)instance!;
                    return new TxBProxy { Value = target.Value };
                });

            var dynamicResult = InvokeDynamicForward(typeof(TxBProxy), typeof(TxBTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(TxBProxy), typeof(TxBTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicValue = dynamicResult.CreateInstance<TxBProxy>(new TxBTarget());
            var aotValue = aotResult.CreateInstance<TxBProxy>(new TxBTarget());

            aotValue.Value.Should().Be(dynamicValue.Value, $"scenario {scenarioId} should preserve property-or-field fallback behavior");
        }

        [Fact]
        public void DifferentialParityTXCMultipleNameFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-C";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxCProxy),
                typeof(TxCTarget),
                typeof(TxCAotProxy),
                instance => new TxCAotProxy((TxCTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxCProxy), typeof(TxCTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxCProxy), typeof(TxCTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxCProxy>(new TxCTarget());
            var aotProxy = aotResult.CreateInstance<ITxCProxy>(new TxCTarget());

            dynamicProxy.Value = "Datadog";
            aotProxy.Value = "Datadog";

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve fallback name resolution");
        }

        [Fact]
        public void DifferentialParityTXDExplicitInterfaceBindingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-D";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxDProxy),
                typeof(TxDTarget),
                typeof(TxDAotProxy),
                instance => new TxDAotProxy((TxDTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxDProxy), typeof(TxDTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxDProxy), typeof(TxDTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxDProxy>(new TxDTarget());
            var aotProxy = aotResult.CreateInstance<ITxDProxy>(new TxDTarget());

            aotProxy.SayHi().Should().Be(dynamicProxy.SayHi(), $"scenario {scenarioId} should preserve explicit interface bindings");
            aotProxy.SayHiWithWildcard().Should().Be(dynamicProxy.SayHiWithWildcard(), $"scenario {scenarioId} should preserve wildcard explicit interface bindings");
        }

        [Fact]
        public void DifferentialParityTXEDuckIncludeObjectMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-E";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxEProxy),
                typeof(TxETarget),
                typeof(TxEAotProxy),
                instance => new TxEAotProxy((TxETarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxEProxy), typeof(TxETarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxEProxy), typeof(TxETarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxEProxy>(new TxETarget());
            var aotProxy = aotResult.CreateInstance<ITxEProxy>(new TxETarget());

            aotProxy.GetHashCode().Should().Be(dynamicProxy.GetHashCode(), $"scenario {scenarioId} should preserve DuckInclude behavior");
        }

        [Fact]
        public void DifferentialParityTXFDuckIgnoreShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-F";
            DuckTypeAotEngine.RegisterProxy(
                typeof(TxFProxyBase),
                typeof(TxFTarget),
                typeof(TxFAotProxy),
                instance => new TxFAotProxy((TxFTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(TxFProxyBase), typeof(TxFTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(TxFProxyBase), typeof(TxFTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<TxFProxyBase>(new TxFTarget("safe"));
            var aotProxy = aotResult.CreateInstance<TxFProxyBase>(new TxFTarget("safe"));

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve non-ignored member behavior");
            aotProxy.GetValue().Should().Be(dynamicProxy.GetValue(), $"scenario {scenarioId} should preserve ignored local implementation behavior");
        }

        [Fact]
        public void DifferentialParityTXGReverseParameterTypeDisambiguationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-G";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(ITxGReverseContract),
                typeof(TxGReverseDelegation),
                typeof(TxGReverseAotProxy),
                instance => new TxGReverseAotProxy((TxGReverseDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(ITxGReverseContract), typeof(TxGReverseDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ITxGReverseContract), typeof(TxGReverseDelegation));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicDelegation = new TxGReverseDelegation();
            var aotDelegation = new TxGReverseDelegation();
            var dynamicProxy = dynamicResult.CreateInstance<ITxGReverseContract>(dynamicDelegation);
            var aotProxy = aotResult.CreateInstance<ITxGReverseContract>(aotDelegation);

            dynamicProxy.Enrich("event", 7);
            aotProxy.Enrich("event", 7);
            aotDelegation.Last.Should().Be(dynamicDelegation.Last, $"scenario {scenarioId} should preserve reverse method selection for int overload");

            dynamicProxy.Enrich("event", "factory");
            aotProxy.Enrich("event", "factory");
            aotDelegation.Last.Should().Be(dynamicDelegation.Last, $"scenario {scenarioId} should preserve reverse method selection for string overload");
        }

        [Fact]
        public void DifferentialParityTXHReverseCustomAttributeCopyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-H";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(ITxHReverseContract),
                typeof(TxHReverseDelegation),
                typeof(TxHReverseAotProxy),
                instance => new TxHReverseAotProxy((TxHReverseDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(ITxHReverseContract), typeof(TxHReverseDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ITxHReverseContract), typeof(TxHReverseDelegation));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicAttribute = dynamicResult.ProxyType!.GetCustomAttribute<TxHMarkerAttribute>();
            var aotAttribute = aotResult.ProxyType!.GetCustomAttribute<TxHMarkerAttribute>();

            aotAttribute.Should().NotBeNull($"scenario {scenarioId} AOT reverse proxy should carry custom attributes");
            dynamicAttribute.Should().NotBeNull($"scenario {scenarioId} dynamic reverse proxy should carry custom attributes");
            aotAttribute!.Value.Should().Be(dynamicAttribute!.Value, $"scenario {scenarioId} should preserve custom attribute values");
        }

        [Fact]
        public void DifferentialParityTXIDuckCopyNullableChainShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-I";
            DuckTypeAotEngine.RegisterProxy(
                typeof(TxIRootCopy),
                typeof(TxIRootTarget),
                typeof(TxIRootCopy),
                instance =>
                {
                    var target = (TxIRootTarget)instance!;
                    return new TxIRootCopy
                    {
                        Value = target.Value is null ? default(TxIInnerCopy?) : new TxIInnerCopy { Name = target.Value.Name }
                    };
                });

            var dynamicResult = InvokeDynamicForward(typeof(TxIRootCopy), typeof(TxIRootTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(TxIRootCopy), typeof(TxIRootTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicNull = dynamicResult.CreateInstance<TxIRootCopy>(new TxIRootTarget(null));
            var aotNull = aotResult.CreateInstance<TxIRootCopy>(new TxIRootTarget(null));
            aotNull.Value.HasValue.Should().Be(dynamicNull.Value.HasValue, $"scenario {scenarioId} null chaining should match");

            var dynamicValue = dynamicResult.CreateInstance<TxIRootCopy>(new TxIRootTarget(new TxIInnerTarget("Hello World")));
            var aotValue = aotResult.CreateInstance<TxIRootCopy>(new TxIRootTarget(new TxIInnerTarget("Hello World")));
            aotValue.Value!.Value.Name.Should().Be(dynamicValue.Value!.Value.Name, $"scenario {scenarioId} nullable duck chaining should match");
        }

        [Fact]
        public void DifferentialParityTXJValueWithTypeReturnWrapperShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-J";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxJValueWithTypeProxy),
                typeof(TxJValueWithTypeTarget),
                typeof(TxJValueWithTypeAotProxy),
                instance => new TxJValueWithTypeAotProxy((TxJValueWithTypeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxJValueWithTypeProxy), typeof(TxJValueWithTypeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxJValueWithTypeProxy), typeof(TxJValueWithTypeTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxJValueWithTypeProxy>(new TxJValueWithTypeTarget());
            var aotProxy = aotResult.CreateInstance<ITxJValueWithTypeProxy>(new TxJValueWithTypeTarget());

            var dynamicValue = dynamicProxy.SumReturnValueWithType(10, 10);
            var aotValue = aotProxy.SumReturnValueWithType(10, 10);
            aotValue.Value.Should().Be(dynamicValue.Value, $"scenario {scenarioId} ValueWithType value should match");
            aotValue.Type.Should().Be(dynamicValue.Type, $"scenario {scenarioId} ValueWithType type should match");
        }

        [Fact]
        public void DifferentialParityTXKRefOutObjectBridgeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-K";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxKMethodProxy),
                typeof(TxKMethodTarget),
                typeof(TxKMethodAotProxy),
                instance => new TxKMethodAotProxy((TxKMethodTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxKMethodProxy), typeof(TxKMethodTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxKMethodProxy), typeof(TxKMethodTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxKMethodProxy>(new TxKMethodTarget());
            var aotProxy = aotResult.CreateInstance<ITxKMethodProxy>(new TxKMethodTarget());

            var dynamicRef = 4;
            var aotRef = 4;
            dynamicProxy.Pow2(ref dynamicRef);
            aotProxy.Pow2(ref aotRef);
            aotRef.Should().Be(dynamicRef, $"scenario {scenarioId} ref update should match");

            dynamicProxy.GetOutputObject(out var dynamicOutput);
            aotProxy.GetOutputObject(out var aotOutput);
            aotOutput.Should().Be(dynamicOutput, $"scenario {scenarioId} out object value should match");

            dynamicProxy.TryGetObscure(out var dynamicDuck);
            aotProxy.TryGetObscure(out var aotDuck);
            aotDuck.Value.Should().Be(dynamicDuck.Value, $"scenario {scenarioId} duck out conversion should match");

            dynamicProxy.TryGetObscureObject(out var dynamicObject);
            aotProxy.TryGetObscureObject(out var aotObject);
            aotObject.Should().BeOfType(dynamicObject.GetType(), $"scenario {scenarioId} object out conversion should match type");
        }

        [Fact]
        public void DifferentialParityTXLParameterTypeNamesDisambiguationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-L";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxLOverloadProxy),
                typeof(TxLOverloadTarget),
                typeof(TxLOverloadAotProxy),
                instance => new TxLOverloadAotProxy((TxLOverloadTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxLOverloadProxy), typeof(TxLOverloadTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxLOverloadProxy), typeof(TxLOverloadTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxLOverloadProxy>(new TxLOverloadTarget());
            var aotProxy = aotResult.CreateInstance<ITxLOverloadProxy>(new TxLOverloadTarget());

            dynamicProxy.AddObject("name", new object());
            aotProxy.AddObject("name", new object());
            aotProxy.Last.Should().Be(dynamicProxy.Last, $"scenario {scenarioId} object overload selection should match");

            dynamicProxy.Add("name", 7);
            aotProxy.Add("name", 7);
            aotProxy.Last.Should().Be(dynamicProxy.Last, $"scenario {scenarioId} int overload selection should match");

            dynamicProxy.Add("name");
            aotProxy.Add("name");
            aotProxy.Last.Should().Be(dynamicProxy.Last, $"scenario {scenarioId} optional parameter overload selection should match");
        }

        [Fact]
        public void DifferentialParityTXMFieldVisibilityMappingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-M";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxMFieldProxy),
                typeof(TxMFieldTarget),
                typeof(TxMFieldAotProxy),
                instance => new TxMFieldAotProxy((TxMFieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxMFieldProxy), typeof(TxMFieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxMFieldProxy), typeof(TxMFieldTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxMFieldProxy>(new TxMFieldTarget());
            var aotProxy = aotResult.CreateInstance<ITxMFieldProxy>(new TxMFieldTarget());

            aotProxy.PublicStaticReadonlyValueTypeField.Should().Be(dynamicProxy.PublicStaticReadonlyValueTypeField, $"scenario {scenarioId} static field mapping should match");

            dynamicProxy.PrivateValueTypeField = 21;
            aotProxy.PrivateValueTypeField = 21;
            aotProxy.PrivateValueTypeField.Should().Be(dynamicProxy.PrivateValueTypeField, $"scenario {scenarioId} private field set/get should match");

            dynamicProxy.PublicNullableIntField = 11;
            aotProxy.PublicNullableIntField = 11;
            aotProxy.PublicNullableIntField.Should().Be(dynamicProxy.PublicNullableIntField, $"scenario {scenarioId} nullable field mapping should match");
        }

        [Fact]
        public void DifferentialParityTXNPropertyIndexerAndValueWithTypeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-N";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxNPropertyProxy),
                typeof(TxNPropertyTarget),
                typeof(TxNPropertyAotProxy),
                instance => new TxNPropertyAotProxy((TxNPropertyTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxNPropertyProxy), typeof(TxNPropertyTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxNPropertyProxy), typeof(TxNPropertyTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxNPropertyProxy>(new TxNPropertyTarget());
            var aotProxy = aotResult.CreateInstance<ITxNPropertyProxy>(new TxNPropertyTarget());

            dynamicProxy.PublicGetSetReferenceType = "value";
            aotProxy.PublicGetSetReferenceType = "value";
            aotProxy.PublicGetSetReferenceType.Should().Be(dynamicProxy.PublicGetSetReferenceType, $"scenario {scenarioId} instance property mapping should match");

            dynamicProxy["k"] = "v";
            aotProxy["k"] = "v";
            aotProxy["k"].Should().Be(dynamicProxy["k"], $"scenario {scenarioId} indexer mapping should match");

            aotProxy.PublicStaticOnlyGetWithType.Value.Should().Be(dynamicProxy.PublicStaticOnlyGetWithType.Value, $"scenario {scenarioId} ValueWithType property value should match");
            aotProxy.PublicStaticOnlyGetWithType.Type.Should().Be(dynamicProxy.PublicStaticOnlyGetWithType.Type, $"scenario {scenarioId} ValueWithType property type should match");
        }

        [Fact]
        public void DifferentialParityTXONullSemanticsShouldMatchCurrentPublicContract()
        {
            const string scenarioId = "TX-O";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxONullProxy),
                typeof(TxONullTarget),
                typeof(TxONullAotProxy),
                instance => new TxONullAotProxy((TxONullTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxONullProxy), typeof(TxONullTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxONullProxy), typeof(TxONullTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxONullProxy>(new TxONullTarget("ok"));
            var aotProxy = aotResult.CreateInstance<ITxONullProxy>(new TxONullTarget("ok"));
            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} non-null proxy behavior should match");

            Action createNull = () => DuckType.Create(typeof(ITxONullProxy), null!);
            createNull.Should().Throw<DuckTypeTargetObjectInstanceIsNull>();

            DuckTypeExtensions.TryDuckCast(null, out ITxONullProxy? castValue).Should().BeFalse();
            castValue.Should().BeNull();
            DuckTypeExtensions.DuckAs<ITxONullProxy>(null).Should().BeNull();
            DuckTypeExtensions.DuckIs<ITxONullProxy>(null).Should().BeFalse();
            DuckTypeExtensions.TryDuckImplement(null, typeof(ITxONullProxy), out var reverse).Should().BeFalse();
            reverse.Should().BeNull();
        }

        [Fact]
        public void DifferentialParityTXPLongTypeNameSafetyPathShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-P";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxPLongNameProxy),
                typeof(TxPTypeNameNesting.TxPMyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName),
                typeof(TxPLongNameAotProxy),
                instance => new TxPLongNameAotProxy(instance!));

            var targetType = typeof(TxPTypeNameNesting.TxPMyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName);
            var dynamicResult = InvokeDynamicForward(typeof(ITxPLongNameProxy), targetType);
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxPLongNameProxy), targetType);

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var target = new TxPTypeNameNesting.TxPMyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName();
            target.GetType().FullName!.Length.Should().BeGreaterThan(200, $"scenario {scenarioId} should exercise a long type name");

            var dynamicProxy = dynamicResult.CreateInstance<ITxPLongNameProxy>(target);
            var aotProxy = aotResult.CreateInstance<ITxPLongNameProxy>(target);
            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} long type-name projection should match");
        }

        [Fact]
        public void DifferentialParityTXQDeepGenericPrivateTypeGraphShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-Q";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxQDeepGenericProxy),
                typeof(TxQDeepGenericTarget),
                typeof(TxQDeepGenericAotProxy),
                instance => new TxQDeepGenericAotProxy((TxQDeepGenericTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxQDeepGenericProxy), typeof(TxQDeepGenericTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxQDeepGenericProxy), typeof(TxQDeepGenericTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<ITxQDeepGenericProxy>(new TxQDeepGenericTarget());
            var aotProxy = aotResult.CreateInstance<ITxQDeepGenericProxy>(new TxQDeepGenericTarget());

            aotProxy.Method.Should().NotBeNull($"scenario {scenarioId} deep generic projection should be created");
            aotProxy.Method.Value.Should().Be(dynamicProxy.Method.Value, $"scenario {scenarioId} deep generic private type projection should match");
        }

        [Fact]
        public void DifferentialParityTXRToStringPassthroughShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-R";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxRToStringProxy),
                typeof(TxRTarget),
                typeof(TxRToStringAotProxy),
                instance => new TxRToStringAotProxy((TxRTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxRToStringProxy), typeof(TxRTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxRToStringProxy), typeof(TxRTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var target = new TxRTarget();
            var dynamicProxy = dynamicResult.CreateInstance<ITxRToStringProxy>(target);
            var aotProxy = aotResult.CreateInstance<ITxRToStringProxy>(target);

            aotProxy.ToString().Should().Be(dynamicProxy.ToString(), $"scenario {scenarioId} ToString passthrough should match");
            aotProxy.ToString().Should().Be(target.ToString(), $"scenario {scenarioId} ToString passthrough should preserve target behavior");
        }

        [Fact]
        public void DifferentialParityTXSReverseDuckChainingPropertyAssignmentShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-S";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(ITxSReversePropertyContract),
                typeof(TxSReversePropertyDelegation),
                typeof(TxSReversePropertyAotProxy),
                instance => new TxSReversePropertyAotProxy((TxSReversePropertyDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(ITxSReversePropertyContract), typeof(TxSReversePropertyDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(ITxSReversePropertyContract), typeof(TxSReversePropertyDelegation));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicDelegation = new TxSReversePropertyDelegation();
            var aotDelegation = new TxSReversePropertyDelegation();
            var dynamicProxy = dynamicResult.CreateInstance<ITxSReversePropertyContract>(dynamicDelegation);
            var aotProxy = aotResult.CreateInstance<ITxSReversePropertyContract>(aotDelegation);

            var dynamicInput = new TxSTestValue();
            var aotInput = new TxSTestValue();
            dynamicProxy.Value = dynamicInput;
            aotProxy.Value = aotInput;

            dynamicDelegation.Value.Should().BeSameAs(dynamicInput, $"scenario {scenarioId} dynamic reverse assignment should preserve original object");
            aotDelegation.Value.Should().BeSameAs(aotInput, $"scenario {scenarioId} AOT reverse assignment should preserve original object");
            aotDelegation.Value!.GetType().Should().Be(dynamicDelegation.Value!.GetType(), $"scenario {scenarioId} reverse assignment storage type should match between modes");
        }

        [Fact]
        public void DifferentialParityTXTOptionalDefaultArgumentFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "TX-T";
            DuckTypeAotEngine.RegisterProxy(
                typeof(ITxTOptionalProxy),
                typeof(TxTOptionalTarget),
                typeof(TxTOptionalAotProxy),
                instance => new TxTOptionalAotProxy((TxTOptionalTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(ITxTOptionalProxy), typeof(TxTOptionalTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITxTOptionalProxy), typeof(TxTOptionalTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicTarget = new TxTOptionalTarget();
            var aotTarget = new TxTOptionalTarget();
            var dynamicProxy = dynamicResult.CreateInstance<ITxTOptionalProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<ITxTOptionalProxy>(aotTarget);

            dynamicProxy.Add("KeyString01");
            aotProxy.Add("KeyString01");

            aotTarget.Last.Should().Be(dynamicTarget.Last, $"scenario {scenarioId} optional fallback should match");
        }

        private static DuckType.CreateTypeResult InvokeDynamicForward(Type proxyDefinitionType, Type targetType)
        {
            if (DynamicForwardFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicProxyType(Type, Type) for bible excerpt parity tests.");
            }

            var result = DynamicForwardFactory.Invoke(null, new object[] { proxyDefinitionType, targetType });
            if (result is DuckType.CreateTypeResult createTypeResult)
            {
                return createTypeResult;
            }

            throw new InvalidOperationException("Dynamic forward factory did not return DuckType.CreateTypeResult.");
        }

        private static DuckType.CreateTypeResult InvokeDynamicReverse(Type typeToDeriveFrom, Type delegationType)
        {
            if (DynamicReverseFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicReverseProxyType(Type, Type) for bible excerpt parity tests.");
            }

            var result = DynamicReverseFactory.Invoke(null, new object[] { typeToDeriveFrom, delegationType });
            if (result is DuckType.CreateTypeResult createTypeResult)
            {
                return createTypeResult;
            }

            throw new InvalidOperationException("Dynamic reverse factory did not return DuckType.CreateTypeResult.");
        }

        private static void AssertCanCreate(string scenarioId, DuckType.CreateTypeResult dynamicResult, DuckType.CreateTypeResult aotResult)
        {
            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");
        }

        private interface ITxADefaultProxy
        {
            string SayHi();
        }

        [DuckAsClass]
        private interface ITxAClassProxy
        {
            string SayHi();
        }

        private class TxATarget
        {
            public string SayHi() => "Hello World";
        }

        private struct TxADefaultAotProxy : ITxADefaultProxy
        {
            private readonly TxATarget _target;

            public TxADefaultAotProxy(TxATarget target)
            {
                _target = target;
            }

            public string SayHi() => _target.SayHi();
        }

        private class TxAClassAotProxy : ITxAClassProxy
        {
            private readonly TxATarget _target;

            public TxAClassAotProxy(TxATarget target)
            {
                _target = target;
            }

            public string SayHi() => _target.SayHi();
        }

        [DuckCopy]
        private struct TxBProxy
        {
            [DuckPropertyOrField(Name = "Value,_value")]
            public int Value;
        }

        private class TxBTarget
        {
            public int _value = 13;

            public int Value => 7;
        }

        private interface ITxCProxy
        {
            [Duck(Name = "Primary,Secondary")]
            string Value { get; set; }
        }

        private class TxCTarget
        {
            public string Secondary { get; set; } = "Datadog";
        }

        private class TxCAotProxy : ITxCProxy
        {
            private readonly TxCTarget _target;

            public TxCAotProxy(TxCTarget target)
            {
                _target = target;
            }

            public string Value
            {
                get => _target.Secondary;
                set => _target.Secondary = value;
            }
        }

        private interface ITxDTargetContract
        {
            string SayHi();

            string SayHiWithWildcard();
        }

        private class TxDTarget : ITxDTargetContract
        {
            string ITxDTargetContract.SayHi() => "hi";

            string ITxDTargetContract.SayHiWithWildcard() => "wildcard";
        }

        private interface ITxDProxy
        {
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.DuckTyping.Tests.DuckTypeAotBibleExcerptsParityTests+ITxDTargetContract")]
            string SayHi();

            [Duck(ExplicitInterfaceTypeName = "*")]
            string SayHiWithWildcard();
        }

        private class TxDAotProxy : ITxDProxy
        {
            private readonly TxDTarget _target;

            public TxDAotProxy(TxDTarget target)
            {
                _target = target;
            }

            public string SayHi() => ((ITxDTargetContract)_target).SayHi();

            public string SayHiWithWildcard() => ((ITxDTargetContract)_target).SayHiWithWildcard();
        }

        private interface ITxEProxy
        {
            [DuckInclude]
            int GetHashCode();
        }

        private class TxETarget
        {
            public override int GetHashCode() => 42;
        }

        private class TxEAotProxy : ITxEProxy
        {
            private readonly TxETarget _target;

            public TxEAotProxy(TxETarget target)
            {
                _target = target;
            }

            public override int GetHashCode() => _target.GetHashCode();
        }

        private abstract class TxFProxyBase
        {
            public abstract string Value { get; }

            [DuckIgnore]
            public string GetValue() => Value;
        }

        private class TxFTarget
        {
            public TxFTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class TxFAotProxy : TxFProxyBase
        {
            private readonly TxFTarget _target;

            public TxFAotProxy(TxFTarget target)
            {
                _target = target;
            }

            public override string Value => _target.Value;
        }

        private interface ITxGReverseContract
        {
            void Enrich(string logEvent, int propertyFactory);

            void Enrich(string logEvent, string propertyFactory);
        }

        private class TxGReverseDelegation
        {
            public string Last { get; private set; } = string.Empty;

            [DuckReverseMethod(Name = "Enrich", ParameterTypeNames = new[] { "System.String", "System.Int32" })]
            public void EnrichWithInt(string logEvent, int propertyFactory)
            {
                Last = $"int:{logEvent}:{propertyFactory}";
            }

            [DuckReverseMethod(Name = "Enrich", ParameterTypeNames = new[] { "System.String", "System.String" })]
            public void EnrichWithString(string logEvent, string propertyFactory)
            {
                Last = $"string:{logEvent}:{propertyFactory}";
            }
        }

        private class TxGReverseAotProxy : ITxGReverseContract
        {
            private readonly TxGReverseDelegation _target;

            public TxGReverseAotProxy(TxGReverseDelegation target)
            {
                _target = target;
            }

            public void Enrich(string logEvent, int propertyFactory)
            {
                _target.EnrichWithInt(logEvent, propertyFactory);
            }

            public void Enrich(string logEvent, string propertyFactory)
            {
                _target.EnrichWithString(logEvent, propertyFactory);
            }
        }

        [AttributeUsage(AttributeTargets.Class)]
        private class TxHMarkerAttribute : Attribute
        {
            public TxHMarkerAttribute(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private interface ITxHReverseContract
        {
            string Value { get; set; }
        }

        [TxHMarker("datadog")]
        private class TxHReverseDelegation
        {
            [DuckReverseMethod(Name = "Value")]
            public string Property { get; set; } = string.Empty;
        }

        [TxHMarker("datadog")]
        private class TxHReverseAotProxy : ITxHReverseContract
        {
            private readonly TxHReverseDelegation _target;

            public TxHReverseAotProxy(TxHReverseDelegation target)
            {
                _target = target;
            }

            public string Value
            {
                get => _target.Property;
                set => _target.Property = value;
            }
        }

        [DuckCopy]
        private struct TxIInnerCopy
        {
            public string Name;
        }

        [DuckCopy]
        private struct TxIRootCopy
        {
            public TxIInnerCopy? Value;
        }

        private class TxIInnerTarget
        {
            public TxIInnerTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class TxIRootTarget
        {
            public TxIRootTarget(TxIInnerTarget? value)
            {
                Value = value;
            }

            public TxIInnerTarget? Value { get; }
        }

        private interface ITxJValueWithTypeProxy
        {
            [Duck(Name = "Sum")]
            ValueWithType<int> SumReturnValueWithType(int a, int b);
        }

        private class TxJValueWithTypeTarget
        {
            public int Sum(int a, int b)
            {
                return a + b;
            }
        }

        private class TxJValueWithTypeAotProxy : ITxJValueWithTypeProxy
        {
            private readonly TxJValueWithTypeTarget _target;

            public TxJValueWithTypeAotProxy(TxJValueWithTypeTarget target)
            {
                _target = target;
            }

            public ValueWithType<int> SumReturnValueWithType(int a, int b)
            {
                return ValueWithType<int>.Create(_target.Sum(a, b), typeof(int));
            }
        }

        private interface ITxKInnerProxy
        {
            string Value { get; }
        }

        private class TxKInnerTarget
        {
            public TxKInnerTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private interface ITxKMethodProxy
        {
            void Pow2(ref int value);

            [Duck(Name = "GetOutput")]
            void GetOutputObject(out object value);

            [Duck(Name = "TryGetDuck")]
            bool TryGetObscure(out ITxKInnerProxy obj);

            [Duck(Name = "TryGetDuck")]
            bool TryGetObscureObject(out object obj);
        }

        private class TxKMethodTarget
        {
            public void Pow2(ref int value)
            {
                value *= value;
            }

            public void GetOutput(out string value)
            {
                value = "output";
            }

            public bool TryGetDuck(out TxKInnerTarget obj)
            {
                obj = new TxKInnerTarget("duck");
                return true;
            }
        }

        private class TxKInnerAotProxy : ITxKInnerProxy
        {
            private readonly TxKInnerTarget _target;

            public TxKInnerAotProxy(TxKInnerTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private class TxKMethodAotProxy : ITxKMethodProxy
        {
            private readonly TxKMethodTarget _target;

            public TxKMethodAotProxy(TxKMethodTarget target)
            {
                _target = target;
            }

            public void Pow2(ref int value)
            {
                _target.Pow2(ref value);
            }

            public void GetOutputObject(out object value)
            {
                _target.GetOutput(out var raw);
                value = raw;
            }

            public bool TryGetObscure(out ITxKInnerProxy obj)
            {
                if (_target.TryGetDuck(out var raw))
                {
                    obj = new TxKInnerAotProxy(raw);
                    return true;
                }

                obj = new TxKInnerAotProxy(new TxKInnerTarget(string.Empty));
                return false;
            }

            public bool TryGetObscureObject(out object obj)
            {
                if (_target.TryGetDuck(out var raw))
                {
                    obj = raw;
                    return true;
                }

                obj = new object();
                return false;
            }
        }

        private interface ITxLOverloadProxy
        {
            [Duck(Name = "Add", ParameterTypeNames = new[] { "System.String", "System.Object" })]
            void AddObject(string name, object obj);

            void Add(string name, int obj);

            void Add(string name, string obj = "none");

            string Last { get; }
        }

        private class TxLOverloadTarget
        {
            public string Last { get; private set; } = string.Empty;

            public void Add(string name, object obj)
            {
                Last = $"object:{name}:{obj.GetType().Name}";
            }

            public void Add(string name, int obj)
            {
                Last = $"int:{name}:{obj}";
            }

            public void Add(string name, string obj = "none")
            {
                Last = $"string:{name}:{obj}";
            }
        }

        private class TxLOverloadAotProxy : ITxLOverloadProxy
        {
            private readonly TxLOverloadTarget _target;

            public TxLOverloadAotProxy(TxLOverloadTarget target)
            {
                _target = target;
            }

            public string Last => _target.Last;

            public void AddObject(string name, object obj)
            {
                _target.Add(name, obj);
            }

            public void Add(string name, int obj)
            {
                _target.Add(name, obj);
            }

            public void Add(string name, string obj = "none")
            {
                _target.Add(name, obj);
            }
        }

        private interface ITxMFieldProxy
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            int PublicStaticReadonlyValueTypeField { get; }

            [DuckField(Name = "_privateValueTypeField")]
            int PrivateValueTypeField { get; set; }

            [DuckField(Name = "_publicNullableIntField")]
            int? PublicNullableIntField { get; set; }
        }

        private class TxMFieldTarget
        {
            public static readonly int _publicStaticReadonlyValueTypeField = 5;

            public int? _publicNullableIntField = 0;

            private int _privateValueTypeField;

            public TxMFieldTarget()
            {
                _privateValueTypeField = 7;
            }

            public int ReadPrivate() => _privateValueTypeField;
        }

        private class TxMFieldAotProxy : ITxMFieldProxy
        {
            private static readonly FieldInfo PublicStaticReadonlyField = typeof(TxMFieldTarget).GetField("_publicStaticReadonlyValueTypeField", BindingFlags.Static | BindingFlags.Public)
                                                                           ?? throw new InvalidOperationException("Unable to resolve _publicStaticReadonlyValueTypeField field.");

            private static readonly FieldInfo PrivateValueTypeFieldInfo = typeof(TxMFieldTarget).GetField("_privateValueTypeField", BindingFlags.Instance | BindingFlags.NonPublic)
                                                                           ?? throw new InvalidOperationException("Unable to resolve _privateValueTypeField field.");

            private static readonly FieldInfo PublicNullableIntFieldInfo = typeof(TxMFieldTarget).GetField("_publicNullableIntField", BindingFlags.Instance | BindingFlags.Public)
                                                                           ?? throw new InvalidOperationException("Unable to resolve _publicNullableIntField field.");

            private readonly TxMFieldTarget _target;

            public TxMFieldAotProxy(TxMFieldTarget target)
            {
                _target = target;
            }

            public int PublicStaticReadonlyValueTypeField => (int)(PublicStaticReadonlyField.GetValue(null) ?? default(int));

            public int PrivateValueTypeField
            {
                get => (int)(PrivateValueTypeFieldInfo.GetValue(_target) ?? default(int));
                set => PrivateValueTypeFieldInfo.SetValue(_target, value);
            }

            public int? PublicNullableIntField
            {
                get => (int?)PublicNullableIntFieldInfo.GetValue(_target);
                set => PublicNullableIntFieldInfo.SetValue(_target, value);
            }
        }

        private interface ITxNPropertyProxy
        {
            string PublicGetSetReferenceType { get; set; }

            [Duck(Name = "PublicStaticGetSetReferenceType")]
            ValueWithType<string> PublicStaticOnlyGetWithType { get; }

            string this[string index] { get; set; }
        }

        private class TxNPropertyTarget
        {
            private readonly Dictionary<string, string> _items = new();

            public static string PublicStaticGetSetReferenceType { get; set; } = "static";

            public string PublicGetSetReferenceType { get; set; } = string.Empty;

            public string this[string index]
            {
                get => _items.TryGetValue(index, out var value) ? value : string.Empty;
                set => _items[index] = value;
            }
        }

        private class TxNPropertyAotProxy : ITxNPropertyProxy
        {
            private readonly TxNPropertyTarget _target;

            public TxNPropertyAotProxy(TxNPropertyTarget target)
            {
                _target = target;
            }

            public string PublicGetSetReferenceType
            {
                get => _target.PublicGetSetReferenceType;
                set => _target.PublicGetSetReferenceType = value;
            }

            public ValueWithType<string> PublicStaticOnlyGetWithType => ValueWithType<string>.Create(TxNPropertyTarget.PublicStaticGetSetReferenceType, typeof(string));

            public string this[string index]
            {
                get => _target[index];
                set => _target[index] = value;
            }
        }

        private interface ITxONullProxy
        {
            string Value { get; }
        }

        private class TxONullTarget
        {
            public TxONullTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class TxONullAotProxy : ITxONullProxy
        {
            private readonly TxONullTarget _target;

            public TxONullAotProxy(TxONullTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface ITxPLongNameProxy
        {
            string Value { get; }
        }

        private class TxPTypeNameNesting
        {
            public class TxPMyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName
            {
                public string Value => "It works!";
            }
        }

        private class TxPLongNameAotProxy : ITxPLongNameProxy
        {
            private static readonly PropertyInfo ValueProperty = typeof(TxPTypeNameNesting.TxPMyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName).GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
                                                                 ?? throw new InvalidOperationException("Unable to resolve long-name Value property.");

            private readonly object _target;

            public TxPLongNameAotProxy(object target)
            {
                _target = target;
            }

            public string Value => (string)(ValueProperty.GetValue(_target) ?? string.Empty);
        }

        private interface ITxQDeepGenericProxy
        {
            ITxQDeepGenericInnerProxy Method { get; }
        }

        private interface ITxQDeepGenericInnerProxy
        {
            string Value { get; }
        }

        private class TxQDeepGenericTarget
        {
            private TxQDeepGenericPrivateType<IEnumerable<Tuple<BoundedConcurrentQueue<MockHttpParser>, Span>>> Method { get; } = new();

            private class TxQDeepGenericPrivateType<T>
            {
                public string Value => typeof(T).ToString();
            }
        }

        private class TxQDeepGenericInnerAotProxy : ITxQDeepGenericInnerProxy
        {
            private static readonly PropertyInfo ValueProperty = typeof(TxQDeepGenericTarget)
                .GetProperty("Method", BindingFlags.Instance | BindingFlags.NonPublic)!
                .PropertyType
                .GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException("Unable to resolve deep generic private Value property.");

            private readonly object _target;

            public TxQDeepGenericInnerAotProxy(object target)
            {
                _target = target;
            }

            public string Value => (string)(ValueProperty.GetValue(_target) ?? string.Empty);
        }

        private class TxQDeepGenericAotProxy : ITxQDeepGenericProxy
        {
            private static readonly PropertyInfo MethodProperty = typeof(TxQDeepGenericTarget).GetProperty("Method", BindingFlags.Instance | BindingFlags.NonPublic)
                                                                     ?? throw new InvalidOperationException("Unable to resolve deep generic Method property.");

            private readonly TxQDeepGenericTarget _target;

            public TxQDeepGenericAotProxy(TxQDeepGenericTarget target)
            {
                _target = target;
            }

            public ITxQDeepGenericInnerProxy Method => new TxQDeepGenericInnerAotProxy(MethodProperty.GetValue(_target)!);
        }

        private interface ITxRToStringProxy
        {
            string ToString();
        }

        private class TxRTarget
        {
            public override string ToString() => "ToString from Target instance.";
        }

        private class TxRToStringAotProxy : ITxRToStringProxy
        {
            private readonly TxRTarget _target;

            public TxRToStringAotProxy(TxRTarget target)
            {
                _target = target;
            }

            public override string ToString() => _target.ToString();
        }

        private class TxSTestValue
        {
        }

        private interface ITxSReversePropertyContract
        {
            TxSTestValue Value { get; set; }
        }

        private class TxSReversePropertyDelegation
        {
            [DuckReverseMethod(Name = "Value")]
            public object? Value { get; set; }
        }

        private class TxSReversePropertyAotProxy : ITxSReversePropertyContract
        {
            private readonly TxSReversePropertyDelegation _target;

            public TxSReversePropertyAotProxy(TxSReversePropertyDelegation target)
            {
                _target = target;
            }

            public TxSTestValue Value
            {
                get
                {
                    if (_target.Value is IDuckType duckType && duckType.Instance is TxSTestValue value)
                    {
                        return value;
                    }

                    return _target.Value as TxSTestValue ?? new TxSTestValue();
                }

                set => _target.Value = value;
            }
        }

        private interface ITxTOptionalProxy
        {
            void Add(string name, string obj = "none");
        }

        private class TxTOptionalTarget
        {
            public string Last { get; private set; } = string.Empty;

            public void Add(string name, string obj = "none")
            {
                Last = $"{name}:{obj}";
            }
        }

        private class TxTOptionalAotProxy : ITxTOptionalProxy
        {
            private readonly TxTOptionalTarget _target;

            public TxTOptionalAotProxy(TxTOptionalTarget target)
            {
                _target = target;
            }

            public void Add(string name, string obj = "none")
            {
                _target.Add(name, obj);
            }
        }
    }
}
