// <copyright file="DuckTypeAotBibleExamplesParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1401 // Fields should be private

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotBibleExamplesParityTests
    {
        private static readonly MethodInfo? DynamicForwardFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicProxyType", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? DynamicReverseFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicReverseProxyType", BindingFlags.NonPublic | BindingFlags.Static);

        public DuckTypeAotBibleExamplesParityTests()
        {
            DuckTypeAotEngine.ResetForTests();
        }

        [Fact]
        public void DifferentialParityEX01BasicForwardInterfaceProxyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-01";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx01HttpProxy),
                typeof(Ex01HttpTarget),
                typeof(Ex01HttpAotProxy),
                instance => new Ex01HttpAotProxy((Ex01HttpTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx01HttpProxy), typeof(Ex01HttpTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx01HttpProxy), typeof(Ex01HttpTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx01HttpProxy>(new Ex01HttpTarget("https://example", 200));
            var aotProxy = aotResult.CreateInstance<IEx01HttpProxy>(new Ex01HttpTarget("https://example", 200));

            aotProxy.Url.Should().Be(dynamicProxy.Url, $"scenario {scenarioId} should preserve Url getter behavior");
            aotProxy.StatusCode.Should().Be(dynamicProxy.StatusCode, $"scenario {scenarioId} should preserve StatusCode getter behavior");
        }

        [Fact]
        public void DifferentialParityEX02FieldMappingPrivateBackingFieldShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-02";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx02ConnectionStringProxy),
                typeof(Ex02ConnectionStringTarget),
                typeof(Ex02ConnectionStringAotProxy),
                instance => new Ex02ConnectionStringAotProxy((Ex02ConnectionStringTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx02ConnectionStringProxy), typeof(Ex02ConnectionStringTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx02ConnectionStringProxy), typeof(Ex02ConnectionStringTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx02ConnectionStringProxy>(new Ex02ConnectionStringTarget("Server=db;Database=trace;"));
            var aotProxy = aotResult.CreateInstance<IEx02ConnectionStringProxy>(new Ex02ConnectionStringTarget("Server=db;Database=trace;"));

            aotProxy.ConnectionString.Should().Be(dynamicProxy.ConnectionString, $"scenario {scenarioId} should preserve private field mapping");
        }

        [Fact]
        public void DifferentialParityEX03PropertyOrFieldFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-03";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx03DataProxy),
                typeof(Ex03DataTarget),
                typeof(Ex03DataAotProxy),
                instance => new Ex03DataAotProxy((Ex03DataTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx03DataProxy), typeof(Ex03DataTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx03DataProxy), typeof(Ex03DataTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx03DataProxy>(new Ex03DataTarget());
            var aotProxy = aotResult.CreateInstance<IEx03DataProxy>(new Ex03DataTarget());

            aotProxy.Data.Should().Be(dynamicProxy.Data, $"scenario {scenarioId} should preserve property-or-field fallback");
        }

        [Fact]
        public void DifferentialParityEX04MethodOverloadDisambiguationShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-04";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx04SerializeProxy),
                typeof(Ex04SerializeTarget),
                typeof(Ex04SerializeAotProxy),
                instance => new Ex04SerializeAotProxy((Ex04SerializeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx04SerializeProxy), typeof(Ex04SerializeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx04SerializeProxy), typeof(Ex04SerializeTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx04SerializeProxy>(new Ex04SerializeTarget());
            var aotProxy = aotResult.CreateInstance<IEx04SerializeProxy>(new Ex04SerializeTarget());

            aotProxy.Serialize("item", 7).Should().Be(dynamicProxy.Serialize("item", 7), $"scenario {scenarioId} should preserve overload selection by parameter type names");
        }

        [Fact]
        public void DifferentialParityEX05GenericMethodOnNonPublicTargetShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-05";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx05GenericBindingProxy),
                typeof(Ex05GenericBindingTarget),
                typeof(Ex05GenericBindingAotProxy),
                instance => new Ex05GenericBindingAotProxy((Ex05GenericBindingTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx05GenericBindingProxy), typeof(Ex05GenericBindingTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx05GenericBindingProxy), typeof(Ex05GenericBindingTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx05GenericBindingProxy>(new Ex05GenericBindingTarget());
            var aotProxy = aotResult.CreateInstance<IEx05GenericBindingProxy>(new Ex05GenericBindingTarget());

            var dynamicTuple = dynamicProxy.CreateIntString(9, "nine");
            var aotTuple = aotProxy.CreateIntString(9, "nine");
            aotTuple.Item1.Should().Be(dynamicTuple.Item1, $"scenario {scenarioId} should preserve generic method int argument");
            aotTuple.Item2.Should().Be(dynamicTuple.Item2, $"scenario {scenarioId} should preserve generic method string argument");
        }

        [Fact]
        public void DifferentialParityEX06ExplicitInterfaceImplementationBindingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-06";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx06ExplicitProxy),
                typeof(Ex06ExplicitTarget),
                typeof(Ex06ExplicitAotProxy),
                instance => new Ex06ExplicitAotProxy((Ex06ExplicitTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx06ExplicitProxy), typeof(Ex06ExplicitTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx06ExplicitProxy), typeof(Ex06ExplicitTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx06ExplicitProxy>(new Ex06ExplicitTarget());
            var aotProxy = aotResult.CreateInstance<IEx06ExplicitProxy>(new Ex06ExplicitTarget());

            aotProxy.MoveNext().Should().Be(dynamicProxy.MoveNext(), $"scenario {scenarioId} should preserve explicit interface method binding");
        }

        [Fact]
        public void DifferentialParityEX07MultiNameFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-07";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx07ExecutorProxy),
                typeof(Ex07ExecutorTarget),
                typeof(Ex07ExecutorAotProxy),
                instance => new Ex07ExecutorAotProxy((Ex07ExecutorTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx07ExecutorProxy), typeof(Ex07ExecutorTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx07ExecutorProxy), typeof(Ex07ExecutorTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicTarget = new Ex07ExecutorTarget();
            var aotTarget = new Ex07ExecutorTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IEx07ExecutorProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IEx07ExecutorProxy>(aotTarget);

            dynamicProxy.Executor = "worker";
            aotProxy.Executor = "worker";
            aotProxy.Executor.Should().Be(dynamicProxy.Executor, $"scenario {scenarioId} should preserve multi-name fallback field selection");
        }

        [Fact]
        public void DifferentialParityEX08ValueWithTypeReturnWrapperShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-08";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx08ValueWithTypeProxy),
                typeof(Ex08ValueWithTypeTarget),
                typeof(Ex08ValueWithTypeAotProxy),
                instance => new Ex08ValueWithTypeAotProxy((Ex08ValueWithTypeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx08ValueWithTypeProxy), typeof(Ex08ValueWithTypeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx08ValueWithTypeProxy), typeof(Ex08ValueWithTypeTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx08ValueWithTypeProxy>(new Ex08ValueWithTypeTarget("payload"));
            var aotProxy = aotResult.CreateInstance<IEx08ValueWithTypeProxy>(new Ex08ValueWithTypeTarget("payload"));

            aotProxy.Payload.Value.Should().Be(dynamicProxy.Payload.Value, $"scenario {scenarioId} should preserve ValueWithType value");
            aotProxy.Payload.Type.Should().Be(dynamicProxy.Payload.Type, $"scenario {scenarioId} should preserve ValueWithType type metadata");
        }

        [Fact]
        public void DifferentialParityEX09DuckCopyStructProjectionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-09";
            DuckTypeAotEngine.RegisterProxy(
                typeof(Ex09RoutePatternStruct),
                typeof(Ex09RoutePatternTarget),
                typeof(Ex09RoutePatternStruct),
                instance =>
                {
                    var target = (Ex09RoutePatternTarget)instance!;
                    return new Ex09RoutePatternStruct { RawText = target.RawText, Defaults = target.Defaults };
                });

            var dynamicResult = InvokeDynamicForward(typeof(Ex09RoutePatternStruct), typeof(Ex09RoutePatternTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(Ex09RoutePatternStruct), typeof(Ex09RoutePatternTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicValue = dynamicResult.CreateInstance<Ex09RoutePatternStruct>(new Ex09RoutePatternTarget("route", 42));
            var aotValue = aotResult.CreateInstance<Ex09RoutePatternStruct>(new Ex09RoutePatternTarget("route", 42));

            aotValue.RawText.Should().Be(dynamicValue.RawText, $"scenario {scenarioId} should preserve DuckCopy string projection");
            aotValue.Defaults.Should().Be(dynamicValue.Defaults, $"scenario {scenarioId} should preserve DuckCopy object projection");
        }

        [Fact]
        public void DifferentialParityEX10ReverseProxyImplementingAbstractContractShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-10";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(Ex10ReverseContract),
                typeof(Ex10ReverseDelegation),
                typeof(Ex10ReverseAotProxy),
                instance => new Ex10ReverseAotProxy((Ex10ReverseDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(Ex10ReverseContract), typeof(Ex10ReverseDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(Ex10ReverseContract), typeof(Ex10ReverseDelegation));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<Ex10ReverseContract>(new Ex10ReverseDelegation("reverse"));
            var aotProxy = aotResult.CreateInstance<Ex10ReverseContract>(new Ex10ReverseDelegation("reverse"));

            aotProxy.GetName().Should().Be(dynamicProxy.GetName(), $"scenario {scenarioId} should preserve reverse abstract method behavior");
        }

        [Fact]
        public void DifferentialParityEX11ReversePropertyMappingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-11";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(Ex11ReversePropertyContract),
                typeof(Ex11ReversePropertyDelegation),
                typeof(Ex11ReversePropertyAotProxy),
                instance => new Ex11ReversePropertyAotProxy((Ex11ReversePropertyDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(Ex11ReversePropertyContract), typeof(Ex11ReversePropertyDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(Ex11ReversePropertyContract), typeof(Ex11ReversePropertyDelegation));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<Ex11ReversePropertyContract>(new Ex11ReversePropertyDelegation());
            var aotProxy = aotResult.CreateInstance<Ex11ReversePropertyContract>(new Ex11ReversePropertyDelegation());

            dynamicProxy.Count = 15;
            aotProxy.Count = 15;
            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve reverse property get/set behavior");
        }

        [Fact]
        public void DifferentialParityEX12SafeProbingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-12";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx12ProbeProxy),
                typeof(Ex12ProbeTarget),
                typeof(Ex12ProbeAotProxy),
                instance => new Ex12ProbeAotProxy((Ex12ProbeTarget)instance!));

            var dynamicSuccessResult = InvokeDynamicForward(typeof(IEx12ProbeProxy), typeof(Ex12ProbeTarget));
            var aotSuccessResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx12ProbeProxy), typeof(Ex12ProbeTarget));
            var dynamicFailResult = InvokeDynamicForward(typeof(IEx12ProbeProxy), typeof(Ex12ProbeMismatchTarget));
            var aotFailResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx12ProbeProxy), typeof(Ex12ProbeMismatchTarget));

            AssertCanCreate(scenarioId, dynamicSuccessResult, aotSuccessResult);
            AssertCannotCreate(scenarioId, dynamicFailResult, aotFailResult);

            TryCreate(dynamicSuccessResult, new Ex12ProbeTarget(5), out IEx12ProbeProxy? dynamicValue).Should().BeTrue();
            TryCreate(aotSuccessResult, new Ex12ProbeTarget(5), out IEx12ProbeProxy? aotValue).Should().BeTrue();
            aotValue!.Value.Should().Be(dynamicValue!.Value, $"scenario {scenarioId} should preserve probing success result");

            TryCreate(dynamicFailResult, new Ex12ProbeMismatchTarget(), out IEx12ProbeProxy? dynamicMissing).Should().BeFalse();
            TryCreate(aotFailResult, new Ex12ProbeMismatchTarget(), out IEx12ProbeProxy? aotMissing).Should().BeFalse();
            dynamicMissing.Should().BeNull();
            aotMissing.Should().BeNull();
        }

        [Fact]
        public void DifferentialParityEX13NullSafeDuckAsProjectionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-13";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx13ProjectionProxy),
                typeof(Ex13ProjectionTarget),
                typeof(Ex13ProjectionAotProxy),
                instance => new Ex13ProjectionAotProxy((Ex13ProjectionTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx13ProjectionProxy), typeof(Ex13ProjectionTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx13ProjectionProxy), typeof(Ex13ProjectionTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicNull = DuckAs(dynamicResult, null);
            var aotNull = DuckAs(aotResult, null);
            dynamicNull.Should().BeNull($"scenario {scenarioId} dynamic null projection should stay null");
            aotNull.Should().BeNull($"scenario {scenarioId} AOT null projection should stay null");

            var dynamicProjection = DuckAs(dynamicResult, new Ex13ProjectionTarget("alpha"));
            var aotProjection = DuckAs(aotResult, new Ex13ProjectionTarget("alpha"));
            aotProjection!.Name.Should().Be(dynamicProjection!.Name, $"scenario {scenarioId} should preserve null-safe projection behavior");
        }

        [Fact]
        public void DifferentialParityEX14IduckTypeNullCheckPatternShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-14";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx14DuckTypeProxy),
                typeof(Ex14DuckTypeTarget),
                typeof(Ex14DuckTypeAotProxy),
                instance => new Ex14DuckTypeAotProxy((Ex14DuckTypeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx14DuckTypeProxy), typeof(Ex14DuckTypeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx14DuckTypeProxy), typeof(Ex14DuckTypeTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx14DuckTypeProxy>(new Ex14DuckTypeTarget("beta"));
            var aotProxy = aotResult.CreateInstance<IEx14DuckTypeProxy>(new Ex14DuckTypeTarget("beta"));

            (((IDuckType)aotProxy).Instance is null).Should().Be(((IDuckType)dynamicProxy).Instance is null, $"scenario {scenarioId} should preserve IDuckType instance null-check semantics");
            aotProxy.Name.Should().Be(dynamicProxy.Name, $"scenario {scenarioId} should preserve proxied member behavior");
        }

        [Fact]
        public void DifferentialParityEX15MethodWithRefAndDuckChainingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-15";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx15TransformProxy),
                typeof(Ex15TransformTarget),
                typeof(Ex15TransformAotProxy),
                instance => new Ex15TransformAotProxy((Ex15TransformTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx15TransformProxy), typeof(Ex15TransformTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx15TransformProxy), typeof(Ex15TransformTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx15TransformProxy>(new Ex15TransformTarget());
            var aotProxy = aotResult.CreateInstance<IEx15TransformProxy>(new Ex15TransformTarget());

            var dynamicValue = 8;
            var aotValue = 8;
            var dynamicInner = dynamicProxy.Transform(ref dynamicValue);
            var aotInner = aotProxy.Transform(ref aotValue);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve ref parameter behavior");
            aotInner.Number.Should().Be(dynamicInner.Number, $"scenario {scenarioId} should preserve duck-chained return behavior");
        }

        [Fact]
        public void DifferentialParityEX16OptionalParameterFallbackShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-16";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx16OptionalProxy),
                typeof(Ex16OptionalTarget),
                typeof(Ex16OptionalAotProxy),
                instance => new Ex16OptionalAotProxy((Ex16OptionalTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx16OptionalProxy), typeof(Ex16OptionalTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx16OptionalProxy), typeof(Ex16OptionalTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx16OptionalProxy>(new Ex16OptionalTarget());
            var aotProxy = aotResult.CreateInstance<IEx16OptionalProxy>(new Ex16OptionalTarget());

            aotProxy.Add("A").Should().Be(dynamicProxy.Add("A"), $"scenario {scenarioId} should preserve optional parameter fallback behavior");
            aotProxy.Add("A", "B").Should().Be(dynamicProxy.Add("A", "B"), $"scenario {scenarioId} should preserve explicit optional argument behavior");
        }

        [Fact]
        public void DifferentialParityEX17StaticFieldAccessShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-17";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx17StaticFieldProxy),
                typeof(Ex17StaticFieldTarget),
                typeof(Ex17StaticFieldAotProxy),
                instance => new Ex17StaticFieldAotProxy((Ex17StaticFieldTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx17StaticFieldProxy), typeof(Ex17StaticFieldTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx17StaticFieldProxy), typeof(Ex17StaticFieldTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx17StaticFieldProxy>(new Ex17StaticFieldTarget());
            var aotProxy = aotResult.CreateInstance<IEx17StaticFieldProxy>(new Ex17StaticFieldTarget());

            Ex17StaticFieldTarget.GlobalCounter = 0;
            dynamicProxy.GlobalCounter = 12;
            var dynamicRead = dynamicProxy.GlobalCounter;

            Ex17StaticFieldTarget.GlobalCounter = 0;
            aotProxy.GlobalCounter = 12;
            var aotRead = aotProxy.GlobalCounter;

            aotRead.Should().Be(dynamicRead, $"scenario {scenarioId} should preserve static field access behavior");
        }

        [Fact]
        public void DifferentialParityEX18DuckAsClassProxyShapeShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-18";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx18DuckAsClassProxy),
                typeof(Ex18DuckAsClassTarget),
                typeof(Ex18DuckAsClassAotProxy),
                instance => new Ex18DuckAsClassAotProxy((Ex18DuckAsClassTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx18DuckAsClassProxy), typeof(Ex18DuckAsClassTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx18DuckAsClassProxy), typeof(Ex18DuckAsClassTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            dynamicResult.ProxyType!.IsClass.Should().BeTrue($"scenario {scenarioId} dynamic proxy must be emitted as class when DuckAsClass is used");
            aotResult.ProxyType!.IsClass.Should().Be(dynamicResult.ProxyType!.IsClass, $"scenario {scenarioId} should preserve proxy shape semantics");
        }

        [Fact]
        public void DifferentialParityEX19DuckIgnoreMemberOmissionShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-19";
            DuckTypeAotEngine.RegisterProxy(
                typeof(Ex19IgnoreProxyBase),
                typeof(Ex19IgnoreTarget),
                typeof(Ex19IgnoreAotProxy),
                instance => new Ex19IgnoreAotProxy((Ex19IgnoreTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(Ex19IgnoreProxyBase), typeof(Ex19IgnoreTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(Ex19IgnoreProxyBase), typeof(Ex19IgnoreTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<Ex19IgnoreProxyBase>(new Ex19IgnoreTarget("safe"));
            var aotProxy = aotResult.CreateInstance<Ex19IgnoreProxyBase>(new Ex19IgnoreTarget("safe"));

            aotProxy.Name.Should().Be(dynamicProxy.Name, $"scenario {scenarioId} should preserve non-ignored member behavior");
            aotProxy.BrokenMember.Should().Be(dynamicProxy.BrokenMember, $"scenario {scenarioId} should preserve ignored-member omission behavior");
        }

        [Fact]
        public void DifferentialParityEX20DuckIncludeObjectLevelMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "EX-20";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IEx20IncludeProxy),
                typeof(Ex20IncludeTarget),
                typeof(Ex20IncludeAotProxy),
                instance => new Ex20IncludeAotProxy((Ex20IncludeTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IEx20IncludeProxy), typeof(Ex20IncludeTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IEx20IncludeProxy), typeof(Ex20IncludeTarget));

            AssertCanCreate(scenarioId, dynamicResult, aotResult);

            var dynamicProxy = dynamicResult.CreateInstance<IEx20IncludeProxy>(new Ex20IncludeTarget());
            var aotProxy = aotResult.CreateInstance<IEx20IncludeProxy>(new Ex20IncludeTarget());

            aotProxy.GetHashCode().Should().Be(dynamicProxy.GetHashCode(), $"scenario {scenarioId} should preserve DuckInclude object-level method behavior");
        }

        private static DuckType.CreateTypeResult InvokeDynamicForward(Type proxyDefinitionType, Type targetType)
        {
            if (DynamicForwardFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicProxyType(Type, Type) for bible parity tests.");
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
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicReverseProxyType(Type, Type) for bible parity tests.");
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

        private static void AssertCannotCreate(string scenarioId, DuckType.CreateTypeResult dynamicResult, DuckType.CreateTypeResult aotResult)
        {
            dynamicResult.CanCreate().Should().BeFalse($"scenario {scenarioId} must be non-creatable in dynamic mode");
            aotResult.CanCreate().Should().BeFalse($"scenario {scenarioId} must be non-creatable in AOT mode");
        }

        private static bool TryCreate<TProxy>(DuckType.CreateTypeResult result, object? instance, out TProxy? value)
            where TProxy : class
        {
            if (instance is null || !result.CanCreate())
            {
                value = default;
                return false;
            }

            value = result.CreateInstance<TProxy>(instance);
            return true;
        }

        private static IEx13ProjectionProxy? DuckAs(DuckType.CreateTypeResult result, object? instance)
        {
            if (instance is null || !result.CanCreate())
            {
                return null;
            }

            return result.CreateInstance<IEx13ProjectionProxy>(instance);
        }

        private interface IEx01HttpProxy
        {
            string Url { get; }

            int StatusCode { get; }
        }

        private class Ex01HttpTarget
        {
            public Ex01HttpTarget(string url, int statusCode)
            {
                Url = url;
                StatusCode = statusCode;
            }

            public string Url { get; }

            public int StatusCode { get; }
        }

        private class Ex01HttpAotProxy : IEx01HttpProxy
        {
            private readonly Ex01HttpTarget _target;

            public Ex01HttpAotProxy(Ex01HttpTarget target)
            {
                _target = target;
            }

            public string Url => _target.Url;

            public int StatusCode => _target.StatusCode;
        }

        private interface IEx02ConnectionStringProxy
        {
            [DuckField(Name = "_connectionString")]
            string ConnectionString { get; }
        }

        private class Ex02ConnectionStringTarget
        {
            private readonly string _connectionString;

            public Ex02ConnectionStringTarget(string connectionString)
            {
                _connectionString = connectionString;
            }
        }

        private class Ex02ConnectionStringAotProxy : IEx02ConnectionStringProxy
        {
            private static readonly FieldInfo ConnectionStringField = typeof(Ex02ConnectionStringTarget).GetField("_connectionString", BindingFlags.Instance | BindingFlags.NonPublic)
                                                                      ?? throw new InvalidOperationException("Unable to resolve _connectionString field.");

            private readonly Ex02ConnectionStringTarget _target;

            public Ex02ConnectionStringAotProxy(Ex02ConnectionStringTarget target)
            {
                _target = target;
            }

            public string ConnectionString => (string)(ConnectionStringField.GetValue(_target) ?? string.Empty);
        }

        private interface IEx03DataProxy
        {
            [DuckPropertyOrField(Name = "Data,_data")]
            object Data { get; }
        }

        private class Ex03DataTarget
        {
            public object _data = "field";

            public object Data => "property";
        }

        private class Ex03DataAotProxy : IEx03DataProxy
        {
            private readonly Ex03DataTarget _target;

            public Ex03DataAotProxy(Ex03DataTarget target)
            {
                _target = target;
            }

            public object Data => _target.Data;
        }

        private interface IEx04SerializeProxy
        {
            [Duck(Name = "Serialize", ParameterTypeNames = new[] { "System.String", "System.Int32" })]
            string Serialize(string name, int payload);
        }

        private class Ex04SerializeTarget
        {
            public string Serialize(string name, string payload)
            {
                return name + ":" + payload;
            }

            public string Serialize(string name, int payload)
            {
                return name + ":" + payload;
            }
        }

        private class Ex04SerializeAotProxy : IEx04SerializeProxy
        {
            private readonly Ex04SerializeTarget _target;

            public Ex04SerializeAotProxy(Ex04SerializeTarget target)
            {
                _target = target;
            }

            public string Serialize(string name, int payload)
            {
                return _target.Serialize(name, payload);
            }
        }

        private interface IEx05GenericBindingProxy
        {
            [Duck(Name = "Create", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
            Tuple<int, string> CreateIntString(int left, string right);
        }

        private class Ex05GenericBindingTarget
        {
            internal Tuple<TLeft, TRight> Create<TLeft, TRight>(TLeft left, TRight right)
            {
                return Tuple.Create(left, right);
            }
        }

        private class Ex05GenericBindingAotProxy : IEx05GenericBindingProxy
        {
            private readonly Ex05GenericBindingTarget _target;

            public Ex05GenericBindingAotProxy(Ex05GenericBindingTarget target)
            {
                _target = target;
            }

            public Tuple<int, string> CreateIntString(int left, string right)
            {
                return _target.Create(left, right);
            }
        }

        private interface IEx06EnumeratorContract
        {
            string MoveNext();
        }

        private interface IEx06ExplicitProxy
        {
            [Duck(Name = "MoveNext", ExplicitInterfaceTypeName = "*")]
            string MoveNext();
        }

        private class Ex06ExplicitTarget : IEx06EnumeratorContract
        {
            string IEx06EnumeratorContract.MoveNext()
            {
                return "moved";
            }
        }

        private class Ex06ExplicitAotProxy : IEx06ExplicitProxy
        {
            private readonly Ex06ExplicitTarget _target;

            public Ex06ExplicitAotProxy(Ex06ExplicitTarget target)
            {
                _target = target;
            }

            public string MoveNext()
            {
                return ((IEx06EnumeratorContract)_target).MoveNext();
            }
        }

        private interface IEx07ExecutorProxy
        {
            [DuckField(Name = "_executor,_methodExecutor")]
            object Executor { get; set; }
        }

        private class Ex07ExecutorTarget
        {
            private object _methodExecutor = "default";
        }

        private class Ex07ExecutorAotProxy : IEx07ExecutorProxy
        {
            private static readonly FieldInfo MethodExecutorField = typeof(Ex07ExecutorTarget).GetField("_methodExecutor", BindingFlags.Instance | BindingFlags.NonPublic)
                                                                    ?? throw new InvalidOperationException("Unable to resolve fallback field _methodExecutor.");

            private readonly Ex07ExecutorTarget _target;

            public Ex07ExecutorAotProxy(Ex07ExecutorTarget target)
            {
                _target = target;
            }

            public object Executor
            {
                get => MethodExecutorField.GetValue(_target) ?? string.Empty;
                set => MethodExecutorField.SetValue(_target, value);
            }
        }

        private interface IEx08ValueWithTypeProxy
        {
            [Duck(Name = "Payload")]
            ValueWithType<object> Payload { get; }
        }

        private class Ex08ValueWithTypeTarget
        {
            public Ex08ValueWithTypeTarget(object payload)
            {
                Payload = payload;
            }

            public object Payload { get; }
        }

        private class Ex08ValueWithTypeAotProxy : IEx08ValueWithTypeProxy
        {
            private readonly Ex08ValueWithTypeTarget _target;

            public Ex08ValueWithTypeAotProxy(Ex08ValueWithTypeTarget target)
            {
                _target = target;
            }

            public ValueWithType<object> Payload => ValueWithType<object>.Create(_target.Payload, typeof(object));
        }

        [DuckCopy]
        private struct Ex09RoutePatternStruct
        {
            public string RawText;
            public object Defaults;
        }

        private class Ex09RoutePatternTarget
        {
            public Ex09RoutePatternTarget(string rawText, object defaults)
            {
                RawText = rawText;
                Defaults = defaults;
            }

            public string RawText { get; }

            public object Defaults { get; }
        }

        private abstract class Ex10ReverseContract
        {
            public abstract string GetName();
        }

        private class Ex10ReverseDelegation
        {
            private readonly string _name;

            public Ex10ReverseDelegation(string name)
            {
                _name = name;
            }

            [DuckReverseMethod(Name = "GetName")]
            public string GetNameReverse()
            {
                return _name;
            }
        }

        private class Ex10ReverseAotProxy : Ex10ReverseContract
        {
            private readonly Ex10ReverseDelegation _delegation;

            public Ex10ReverseAotProxy(Ex10ReverseDelegation delegation)
            {
                _delegation = delegation;
            }

            public override string GetName()
            {
                return _delegation.GetNameReverse();
            }
        }

        private abstract class Ex11ReversePropertyContract
        {
            public abstract int Count { get; set; }
        }

        private class Ex11ReversePropertyDelegation
        {
            [DuckReverseMethod]
            public int Count { get; set; }
        }

        private class Ex11ReversePropertyAotProxy : Ex11ReversePropertyContract
        {
            private readonly Ex11ReversePropertyDelegation _delegation;

            public Ex11ReversePropertyAotProxy(Ex11ReversePropertyDelegation delegation)
            {
                _delegation = delegation;
            }

            public override int Count
            {
                get => _delegation.Count;
                set => _delegation.Count = value;
            }
        }

        private interface IEx12ProbeProxy
        {
            int Value { get; }
        }

        private class Ex12ProbeTarget
        {
            public Ex12ProbeTarget(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private class Ex12ProbeMismatchTarget
        {
            public string Value => "mismatch";
        }

        private class Ex12ProbeAotProxy : IEx12ProbeProxy
        {
            private readonly Ex12ProbeTarget _target;

            public Ex12ProbeAotProxy(Ex12ProbeTarget target)
            {
                _target = target;
            }

            public int Value => _target.Value;
        }

        private interface IEx13ProjectionProxy
        {
            string Name { get; }
        }

        private class Ex13ProjectionTarget
        {
            public Ex13ProjectionTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class Ex13ProjectionAotProxy : IEx13ProjectionProxy
        {
            private readonly Ex13ProjectionTarget _target;

            public Ex13ProjectionAotProxy(Ex13ProjectionTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private interface IEx14DuckTypeProxy : IDuckType
        {
            string Name { get; }
        }

        private class Ex14DuckTypeTarget
        {
            public Ex14DuckTypeTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class Ex14DuckTypeAotProxy : IEx14DuckTypeProxy
        {
            private readonly Ex14DuckTypeTarget _target;

            public Ex14DuckTypeAotProxy(Ex14DuckTypeTarget target)
            {
                _target = target;
            }

            public Type Type => _target.GetType();

            public object? Instance => _target;

            public string Name => _target.Name;

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return _target.ToString() ?? string.Empty;
            }
        }

        private interface IEx15InnerProxy
        {
            int Number { get; }
        }

        private class Ex15InnerTarget
        {
            public Ex15InnerTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class Ex15InnerAotProxy : IEx15InnerProxy
        {
            private readonly Ex15InnerTarget _target;

            public Ex15InnerAotProxy(Ex15InnerTarget target)
            {
                _target = target;
            }

            public int Number => _target.Number;
        }

        private interface IEx15TransformProxy
        {
            IEx15InnerProxy Transform(ref int value);
        }

        private class Ex15TransformTarget
        {
            public Ex15InnerTarget Transform(ref int value)
            {
                value += 10;
                return new Ex15InnerTarget(value);
            }
        }

        private class Ex15TransformAotProxy : IEx15TransformProxy
        {
            private readonly Ex15TransformTarget _target;

            public Ex15TransformAotProxy(Ex15TransformTarget target)
            {
                _target = target;
            }

            public IEx15InnerProxy Transform(ref int value)
            {
                return new Ex15InnerAotProxy(_target.Transform(ref value));
            }
        }

        private interface IEx16OptionalProxy
        {
            string Add(string left, string right = "default");
        }

        private class Ex16OptionalTarget
        {
            public string Add(string left, string right = "default")
            {
                return left + ":" + right;
            }
        }

        private class Ex16OptionalAotProxy : IEx16OptionalProxy
        {
            private readonly Ex16OptionalTarget _target;

            public Ex16OptionalAotProxy(Ex16OptionalTarget target)
            {
                _target = target;
            }

            public string Add(string left, string right = "default")
            {
                return _target.Add(left, right);
            }
        }

        private interface IEx17StaticFieldProxy
        {
            [DuckField(Name = "GlobalCounter")]
            int GlobalCounter { get; set; }
        }

        private class Ex17StaticFieldTarget
        {
            public static int GlobalCounter = 0;
        }

        private class Ex17StaticFieldAotProxy : IEx17StaticFieldProxy
        {
            public Ex17StaticFieldAotProxy(Ex17StaticFieldTarget target)
            {
                _ = target;
            }

            public int GlobalCounter
            {
                get => Ex17StaticFieldTarget.GlobalCounter;
                set => Ex17StaticFieldTarget.GlobalCounter = value;
            }
        }

        [DuckAsClass]
        private interface IEx18DuckAsClassProxy
        {
            string Name { get; }
        }

        private class Ex18DuckAsClassTarget
        {
            public Ex18DuckAsClassTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class Ex18DuckAsClassAotProxy : IEx18DuckAsClassProxy
        {
            private readonly Ex18DuckAsClassTarget _target;

            public Ex18DuckAsClassAotProxy(Ex18DuckAsClassTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private abstract class Ex19IgnoreProxyBase
        {
            public abstract string Name { get; }

            [DuckIgnore]
            public virtual string BrokenMember => "ignored";
        }

        private class Ex19IgnoreTarget
        {
            public Ex19IgnoreTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public string BrokenMember => throw new InvalidOperationException("Broken member should be ignored.");
        }

        private class Ex19IgnoreAotProxy : Ex19IgnoreProxyBase
        {
            private readonly Ex19IgnoreTarget _target;

            public Ex19IgnoreAotProxy(Ex19IgnoreTarget target)
            {
                _target = target;
            }

            public override string Name => _target.Name;
        }

        private interface IEx20IncludeProxy
        {
            [DuckInclude]
            int GetHashCode();
        }

        private class Ex20IncludeTarget
        {
            public override int GetHashCode()
            {
                return 42;
            }
        }

        private class Ex20IncludeAotProxy : IEx20IncludeProxy
        {
            private readonly Ex20IncludeTarget _target;

            public Ex20IncludeAotProxy(Ex20IncludeTarget target)
            {
                _target = target;
            }

            public override int GetHashCode()
            {
                return _target.GetHashCode();
            }
        }
    }
}
