// <copyright file="DuckTypeAotDifferentialParityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotDifferentialParityTests
    {
        private static readonly MethodInfo? DynamicForwardFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicProxyType", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? DynamicReverseFactory = typeof(DuckType).GetMethod("GetOrCreateDynamicReverseProxyType", BindingFlags.NonPublic | BindingFlags.Static);

        public DuckTypeAotDifferentialParityTests()
        {
            DuckTypeAotEngine.ResetForTests();
        }

        [Fact]
        public void DifferentialParityA01ForwardPropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-01";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardValueProxy),
                typeof(ForwardValueTarget),
                typeof(ForwardValueAotProxy),
                instance => new ForwardValueAotProxy((ForwardValueTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardValueProxy), typeof(ForwardValueTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardValueProxy), typeof(ForwardValueTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new ForwardValueTarget("alpha");
            var dynamicProxy = dynamicResult.CreateInstance<IForwardValueProxy>(target);
            var aotProxy = aotResult.CreateInstance<IForwardValueProxy>(target);

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should produce equivalent values");
        }

        [Fact]
        public void DifferentialParityA02ForwardMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-02";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardMathProxy),
                typeof(ForwardMathTarget),
                typeof(ForwardMathAotProxy),
                instance => new ForwardMathAotProxy((ForwardMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardMathProxy), typeof(ForwardMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardMathProxy), typeof(ForwardMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var target = new ForwardMathTarget();
            var dynamicProxy = dynamicResult.CreateInstance<IForwardMathProxy>(target);
            var aotProxy = aotResult.CreateInstance<IForwardMathProxy>(target);

            aotProxy.Add(3, 9).Should().Be(dynamicProxy.Add(3, 9), $"scenario {scenarioId} should produce equivalent method results");
            aotProxy.Add(-4, 2).Should().Be(dynamicProxy.Add(-4, 2), $"scenario {scenarioId} should preserve behavior across different inputs");
        }

        [Fact]
        public void DifferentialParityD34ReverseInterfaceShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-34";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseGreetingProxy),
                typeof(ReverseGreetingDelegation),
                typeof(ReverseGreetingAotProxy),
                instance => new ReverseGreetingAotProxy((ReverseGreetingDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseGreetingProxy), typeof(ReverseGreetingDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseGreetingProxy), typeof(ReverseGreetingDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var delegation = new ReverseGreetingDelegation("beta");
            var dynamicProxy = dynamicResult.CreateInstance<IReverseGreetingProxy>(delegation);
            var aotProxy = aotResult.CreateInstance<IReverseGreetingProxy>(delegation);

            aotProxy.Greet().Should().Be(dynamicProxy.Greet(), $"scenario {scenarioId} should produce equivalent reverse proxy behavior");
        }

        private static DuckType.CreateTypeResult InvokeDynamicForward(Type proxyDefinitionType, Type targetType)
        {
            if (DynamicForwardFactory is null)
            {
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicProxyType(Type, Type) for differential parity tests.");
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
                throw new InvalidOperationException("Unable to resolve DuckType.GetOrCreateDynamicReverseProxyType(Type, Type) for differential parity tests.");
            }

            var result = DynamicReverseFactory.Invoke(null, new object[] { typeToDeriveFrom, delegationType });
            if (result is DuckType.CreateTypeResult createTypeResult)
            {
                return createTypeResult;
            }

            throw new InvalidOperationException("Dynamic reverse factory did not return DuckType.CreateTypeResult.");
        }

        private interface IForwardValueProxy
        {
            string Value { get; }
        }

        private class ForwardValueTarget
        {
            public ForwardValueTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ForwardValueAotProxy : IForwardValueProxy
        {
            private readonly ForwardValueTarget _target;

            public ForwardValueAotProxy(ForwardValueTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IForwardMathProxy
        {
            int Add(int left, int right);
        }

        private class ForwardMathTarget
        {
            public int Add(int left, int right)
            {
                return left + right;
            }
        }

        private class ForwardMathAotProxy : IForwardMathProxy
        {
            private readonly ForwardMathTarget _target;

            public ForwardMathAotProxy(ForwardMathTarget target)
            {
                _target = target;
            }

            public int Add(int left, int right)
            {
                return _target.Add(left, right);
            }
        }

        private interface IReverseGreetingProxy
        {
            string Greet();
        }

        private class ReverseGreetingDelegation
        {
            private readonly string _name;

            public ReverseGreetingDelegation(string name)
            {
                _name = name;
            }

            [DuckReverseMethod]
            public string Greet()
            {
                return "hello " + _name;
            }
        }

        private class ReverseGreetingAotProxy : IReverseGreetingProxy
        {
            private readonly ReverseGreetingDelegation _delegation;

            public ReverseGreetingAotProxy(ReverseGreetingDelegation delegation)
            {
                _delegation = delegation;
            }

            public string Greet()
            {
                return _delegation.Greet();
            }
        }
    }
}
