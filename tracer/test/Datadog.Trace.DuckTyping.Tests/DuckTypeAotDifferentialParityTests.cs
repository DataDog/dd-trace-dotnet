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
#pragma warning disable SA1401 // Fields should be private

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
        public void DifferentialParityA03ForwardPropertySetShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-03";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardMutableValueProxy),
                typeof(ForwardMutableValueTarget),
                typeof(ForwardMutableValueAotProxy),
                instance => new ForwardMutableValueAotProxy((ForwardMutableValueTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IForwardMutableValueProxy), typeof(ForwardMutableValueTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardMutableValueProxy), typeof(ForwardMutableValueTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new ForwardMutableValueTarget();
            var aotTarget = new ForwardMutableValueTarget();

            var dynamicProxy = dynamicResult.CreateInstance<IForwardMutableValueProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IForwardMutableValueProxy>(aotTarget);

            dynamicProxy.Value = "delta";
            aotProxy.Value = "delta";

            aotProxy.Value.Should().Be(dynamicProxy.Value, $"scenario {scenarioId} should preserve proxy setter/getter behavior");
            aotTarget.Value.Should().Be(dynamicTarget.Value, $"scenario {scenarioId} should apply the same target mutation");
        }

        [Fact]
        public void DifferentialParityA04ForwardRefOutMethodShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "A-04";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IRefOutMathProxy),
                typeof(RefOutMathTarget),
                typeof(RefOutMathAotProxy),
                instance => new RefOutMathAotProxy((RefOutMathTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IRefOutMathProxy), typeof(RefOutMathTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IRefOutMathProxy), typeof(RefOutMathTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IRefOutMathProxy>(new RefOutMathTarget());
            var aotProxy = aotResult.CreateInstance<IRefOutMathProxy>(new RefOutMathTarget());

            var dynamicValue = -7;
            var aotValue = -7;

            dynamicProxy.Normalize(ref dynamicValue, out var dynamicDoubled);
            aotProxy.Normalize(ref aotValue, out var aotDoubled);

            aotValue.Should().Be(dynamicValue, $"scenario {scenarioId} should preserve ref parameter behavior");
            aotDoubled.Should().Be(dynamicDoubled, $"scenario {scenarioId} should preserve out parameter behavior");
        }

        [Fact]
        public void DifferentialParityB16ForwardFieldBackedPropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "B-16";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IFieldCountProxy),
                typeof(FieldCountTarget),
                typeof(FieldCountAotProxy),
                instance => new FieldCountAotProxy((FieldCountTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IFieldCountProxy), typeof(FieldCountTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IFieldCountProxy), typeof(FieldCountTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicTarget = new FieldCountTarget();
            var aotTarget = new FieldCountTarget();

            var dynamicProxy = dynamicResult.CreateInstance<IFieldCountProxy>(dynamicTarget);
            var aotProxy = aotResult.CreateInstance<IFieldCountProxy>(aotTarget);

            dynamicProxy.Count = 11;
            aotProxy.Count = 11;

            aotProxy.Count.Should().Be(dynamicProxy.Count, $"scenario {scenarioId} should preserve field-backed value behavior");
            aotTarget.Count.Should().Be(dynamicTarget.Count, $"scenario {scenarioId} should mutate field-backed targets equivalently");
        }

        [Fact]
        public void DifferentialParityC28ForwardDuckChainingShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "C-28";
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainInnerProxy),
                typeof(ChainInnerTarget),
                typeof(ChainInnerAotProxy),
                instance => new ChainInnerAotProxy((ChainInnerTarget)instance!));
            DuckTypeAotEngine.RegisterProxy(
                typeof(IChainOuterProxy),
                typeof(ChainOuterTarget),
                typeof(ChainOuterAotProxy),
                instance => new ChainOuterAotProxy((ChainOuterTarget)instance!));

            var dynamicResult = InvokeDynamicForward(typeof(IChainOuterProxy), typeof(ChainOuterTarget));
            var aotResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IChainOuterProxy), typeof(ChainOuterTarget));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IChainOuterProxy>(new ChainOuterTarget(new ChainInnerTarget("chain")));
            var aotProxy = aotResult.CreateInstance<IChainOuterProxy>(new ChainOuterTarget(new ChainInnerTarget("chain")));

            aotProxy.Inner.Should().NotBeNull();
            dynamicProxy.Inner.Should().NotBeNull();
            aotProxy.Inner.Name.Should().Be(dynamicProxy.Inner.Name, $"scenario {scenarioId} should preserve chained proxy behavior");
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

        [Fact]
        public void DifferentialParityD35ReversePropertyShouldMatchBetweenDynamicAndAot()
        {
            const string scenarioId = "D-35";
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseStateProxy),
                typeof(ReverseStateDelegation),
                typeof(ReverseStateAotProxy),
                instance => new ReverseStateAotProxy((ReverseStateDelegation)instance!));

            var dynamicResult = InvokeDynamicReverse(typeof(IReverseStateProxy), typeof(ReverseStateDelegation));
            var aotResult = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseStateProxy), typeof(ReverseStateDelegation));

            dynamicResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in dynamic mode");
            aotResult.CanCreate().Should().BeTrue($"scenario {scenarioId} must be creatable in AOT mode");

            var dynamicProxy = dynamicResult.CreateInstance<IReverseStateProxy>(new ReverseStateDelegation());
            var aotProxy = aotResult.CreateInstance<IReverseStateProxy>(new ReverseStateDelegation());

            dynamicProxy.State = 21;
            aotProxy.State = 21;

            aotProxy.State.Should().Be(dynamicProxy.State, $"scenario {scenarioId} should preserve reverse proxy property behavior");
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

        private interface IForwardMutableValueProxy
        {
            string Value { get; set; }
        }

        private class ForwardMutableValueTarget
        {
            public string Value { get; set; } = string.Empty;
        }

        private class ForwardMutableValueAotProxy : IForwardMutableValueProxy
        {
            private readonly ForwardMutableValueTarget _target;

            public ForwardMutableValueAotProxy(ForwardMutableValueTarget target)
            {
                _target = target;
            }

            public string Value
            {
                get => _target.Value;
                set => _target.Value = value;
            }
        }

        private interface IRefOutMathProxy
        {
            void Normalize(ref int value, out int doubled);
        }

        private class RefOutMathTarget
        {
            public void Normalize(ref int value, out int doubled)
            {
                value = Math.Abs(value);
                doubled = value * 2;
            }
        }

        private class RefOutMathAotProxy : IRefOutMathProxy
        {
            private readonly RefOutMathTarget _target;

            public RefOutMathAotProxy(RefOutMathTarget target)
            {
                _target = target;
            }

            public void Normalize(ref int value, out int doubled)
            {
                _target.Normalize(ref value, out doubled);
            }
        }

        private interface IFieldCountProxy
        {
            [DuckPropertyOrField]
            int Count { get; set; }
        }

        private class FieldCountTarget
        {
            public int Count;
        }

        private class FieldCountAotProxy : IFieldCountProxy
        {
            private readonly FieldCountTarget _target;

            public FieldCountAotProxy(FieldCountTarget target)
            {
                _target = target;
            }

            public int Count
            {
                get => _target.Count;
                set => _target.Count = value;
            }
        }

        private interface IChainInnerProxy
        {
            string Name { get; }
        }

        private class ChainInnerTarget
        {
            public ChainInnerTarget(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class ChainInnerAotProxy : IChainInnerProxy
        {
            private readonly ChainInnerTarget _target;

            public ChainInnerAotProxy(ChainInnerTarget target)
            {
                _target = target;
            }

            public string Name => _target.Name;
        }

        private interface IChainOuterProxy
        {
            IChainInnerProxy Inner { get; }
        }

        private class ChainOuterTarget
        {
            public ChainOuterTarget(ChainInnerTarget inner)
            {
                Inner = inner;
            }

            public ChainInnerTarget Inner { get; }
        }

        private class ChainOuterAotProxy : IChainOuterProxy
        {
            private readonly ChainOuterTarget _target;

            public ChainOuterAotProxy(ChainOuterTarget target)
            {
                _target = target;
            }

            public IChainInnerProxy Inner => new ChainInnerAotProxy(_target.Inner);
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

        private interface IReverseStateProxy
        {
            int State { get; set; }
        }

        private class ReverseStateDelegation
        {
            [DuckReverseMethod]
            public int State { get; set; }
        }

        private class ReverseStateAotProxy : IReverseStateProxy
        {
            private readonly ReverseStateDelegation _delegation;

            public ReverseStateAotProxy(ReverseStateDelegation delegation)
            {
                _delegation = delegation;
            }

            public int State
            {
                get => _delegation.State;
                set => _delegation.State = value;
            }
        }
    }
}
