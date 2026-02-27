// <copyright file="DuckTypeAotEngineTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotEngineTests
    {
        public DuckTypeAotEngineTests()
        {
            DuckTypeAotEngine.ResetForTests();
        }

        [Fact]
        public void MissingMappingReturnsErrorResult()
        {
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IMissingProxy), typeof(MissingTarget));

            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void RegisterForwardProxyAndResolve()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                instance => new ForwardGeneratedProxy((ForwardTarget)instance!));

            var target = new ForwardTarget("hello");
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IForwardProxy>(target).Value.Should().Be("hello");
        }

        [Fact]
        public void DuplicateRegistrationIsIdempotent()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(IDuplicateProxy),
                typeof(DuplicateTarget),
                typeof(DuplicateGeneratedProxy),
                instance => new DuplicateGeneratedProxy((DuplicateTarget)instance!));

            Action secondRegistration = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IDuplicateProxy),
                typeof(DuplicateTarget),
                typeof(DuplicateGeneratedProxy),
                instance => new DuplicateGeneratedProxy((DuplicateTarget)instance!));

            secondRegistration.Should().NotThrow();
        }

        [Fact]
        public void ConflictingRegistrationThrows()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(IConflictProxy),
                typeof(ConflictTarget),
                typeof(ConflictGeneratedProxy),
                instance => new ConflictGeneratedProxy((ConflictTarget)instance!));

            Action conflictingRegistration = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IConflictProxy),
                typeof(ConflictTarget),
                typeof(ConflictGeneratedProxy2),
                instance => new ConflictGeneratedProxy2((ConflictTarget)instance!));

            conflictingRegistration.Should().Throw<DuckTypeAotProxyRegistrationConflictException>();
        }

        [Fact]
        public void LateRegistrationInvalidatesMissCache()
        {
            var missingResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ILateProxy), typeof(LateTarget));
            missingResult.CanCreate().Should().BeFalse();

            DuckTypeAotEngine.RegisterProxy(
                typeof(ILateProxy),
                typeof(LateTarget),
                typeof(LateGeneratedProxy),
                instance => new LateGeneratedProxy((LateTarget)instance!));

            var resolvedResult = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ILateProxy), typeof(LateTarget));
            resolvedResult.CanCreate().Should().BeTrue();
            resolvedResult.CreateInstance<ILateProxy>(new LateTarget(42)).Number.Should().Be(42);
        }

        [Fact]
        public void RegisterReverseProxyAndResolve()
        {
            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                typeof(ReverseGeneratedProxy),
                instance => new ReverseGeneratedProxy((ReverseTarget)instance!));

            var result = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseProxy), typeof(ReverseTarget));
            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IReverseProxy>(new ReverseTarget("reverse")).Value.Should().Be("reverse");
        }

        [Fact]
        public void RegisterProxyWithIncompatibleGeneratedTypeThrows()
        {
            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IInvalidGeneratedProxy),
                typeof(InvalidGeneratedTarget),
                typeof(InvalidGeneratedProxyType),
                _ => new InvalidGeneratedProxyType());

            register.Should().Throw<DuckTypeAotGeneratedProxyTypeMismatchException>();
        }

        [Fact]
        public void RegisterProxyFromDifferentRegistryAssemblyThrows()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(ISingleRegistryWarmupProxy),
                typeof(SingleRegistryWarmupTarget),
                typeof(SingleRegistryWarmupGeneratedProxy),
                instance => new SingleRegistryWarmupGeneratedProxy((SingleRegistryWarmupTarget)instance!));

            var dynamicActivator = CreateDynamicAssemblyActivator();
            Action conflictingRegistryRegistration = () => DuckTypeAotEngine.RegisterProxy(
                typeof(ISingleRegistryConflictProxy),
                typeof(SingleRegistryConflictTarget),
                typeof(SingleRegistryConflictGeneratedProxy),
                dynamicActivator);

            conflictingRegistryRegistration.Should().Throw<DuckTypeAotMultipleRegistryAssembliesException>();
        }

        private interface IMissingProxy
        {
            string Value { get; }
        }

        private class MissingTarget
        {
        }

        private interface IForwardProxy
        {
            string Value { get; }
        }

        private class ForwardTarget
        {
            public ForwardTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ForwardGeneratedProxy : IForwardProxy
        {
            private readonly ForwardTarget _target;

            public ForwardGeneratedProxy(ForwardTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IDuplicateProxy
        {
            string Value { get; }
        }

        private class DuplicateTarget
        {
            public DuplicateTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class DuplicateGeneratedProxy : IDuplicateProxy
        {
            private readonly DuplicateTarget _target;

            public DuplicateGeneratedProxy(DuplicateTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IConflictProxy
        {
            string Value { get; }
        }

        private class ConflictTarget
        {
            public ConflictTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ConflictGeneratedProxy : IConflictProxy
        {
            private readonly ConflictTarget _target;

            public ConflictGeneratedProxy(ConflictTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private class ConflictGeneratedProxy2 : IConflictProxy
        {
            private readonly ConflictTarget _target;

            public ConflictGeneratedProxy2(ConflictTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface ILateProxy
        {
            int Number { get; }
        }

        private class LateTarget
        {
            public LateTarget(int number)
            {
                Number = number;
            }

            public int Number { get; }
        }

        private class LateGeneratedProxy : ILateProxy
        {
            private readonly LateTarget _target;

            public LateGeneratedProxy(LateTarget target)
            {
                _target = target;
            }

            public int Number => _target.Number;
        }

        private interface IReverseProxy
        {
            string Value { get; }
        }

        private class ReverseTarget
        {
            public ReverseTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ReverseGeneratedProxy : IReverseProxy
        {
            private readonly ReverseTarget _target;

            public ReverseGeneratedProxy(ReverseTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IInvalidGeneratedProxy
        {
            string Value { get; }
        }

        private class InvalidGeneratedTarget
        {
        }

        private class InvalidGeneratedProxyType
        {
        }

        private interface ISingleRegistryWarmupProxy
        {
            string Value { get; }
        }

        private class SingleRegistryWarmupTarget
        {
            public SingleRegistryWarmupTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class SingleRegistryWarmupGeneratedProxy : ISingleRegistryWarmupProxy
        {
            private readonly SingleRegistryWarmupTarget _target;

            public SingleRegistryWarmupGeneratedProxy(SingleRegistryWarmupTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface ISingleRegistryConflictProxy
        {
            string Value { get; }
        }

        private class SingleRegistryConflictTarget
        {
            public SingleRegistryConflictTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class SingleRegistryConflictGeneratedProxy : ISingleRegistryConflictProxy
        {
            private readonly SingleRegistryConflictTarget _target;

            public SingleRegistryConflictGeneratedProxy(SingleRegistryConflictTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private static Func<object?, object?> CreateDynamicAssemblyActivator()
        {
            var ctor = typeof(SingleRegistryConflictGeneratedProxy).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(SingleRegistryConflictTarget)],
                modifiers: null);
            ctor.Should().NotBeNull();

            var dynamicMethod = new DynamicMethod(
                "CreateSingleRegistryConflictProxy",
                typeof(object),
                [typeof(object)]);
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, typeof(SingleRegistryConflictTarget));
            il.Emit(OpCodes.Newobj, ctor!);
            il.Emit(OpCodes.Ret);
            return (Func<object?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?>));
        }
    }
}
