// <copyright file="DuckTypeAotEngineTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable CS0618 // Manual AOT registration APIs are deprecated but remain covered by compatibility tests.

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class DuckTypeAotEngineTests : IDisposable
    {
        public DuckTypeAotEngineTests()
        {
            DuckType.ResetRuntimeModeForTests();
        }

        public void Dispose()
        {
            DuckType.ResetRuntimeModeForTests();
            DuckTypeTestRuntimeBootstrap.ReinitializeAotRegistryForTests();
        }

        [Fact]
        public void RuntimeModeShouldBeImmutableAfterDynamicInitialization()
        {
            var dynamicResult = DuckType.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
            dynamicResult.CanCreate().Should().BeTrue();

            Action enableAot = DuckType.EnableAotMode;
            enableAot.Should().Throw<DuckTypeRuntimeModeConflictException>();
            DuckType.IsAotMode().Should().BeFalse();
        }

        [Fact]
        public void RuntimeModeShouldBeImmutableAfterAotInitialization()
        {
            DuckType.EnableAotMode();
            DuckType.IsAotMode().Should().BeTrue();

            Action enableAotAgain = DuckType.EnableAotMode;
            enableAotAgain.Should().NotThrow();

            var result = DuckType.GetOrCreateProxyType(typeof(IMissingProxy), typeof(MissingTarget));
            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void RegisterAotProxyAfterDynamicInitializationShouldThrowModeConflict()
        {
            var dynamicResult = DuckType.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
            dynamicResult.CanCreate().Should().BeTrue();

            Action register = () => DuckType.RegisterAotProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                instance => new ForwardGeneratedProxy((ForwardTarget)instance!));

            register.Should().Throw<DuckTypeRuntimeModeConflictException>();
        }

        [Fact]
        public void RegisterAotReverseProxyAfterDynamicInitializationShouldThrowModeConflict()
        {
            var dynamicResult = DuckType.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
            dynamicResult.CanCreate().Should().BeTrue();

            Action register = () => DuckType.RegisterAotReverseProxy(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                typeof(ReverseGeneratedProxy),
                instance => new ReverseGeneratedProxy((ReverseTarget)instance!));

            register.Should().Throw<DuckTypeRuntimeModeConflictException>();
        }

        [Fact]
        public async Task EnableAotModeShouldBeThreadSafeUnderConcurrentCalls()
        {
            var startGate = new ManualResetEventSlim(initialState: false);
            var exceptions = new ConcurrentQueue<Exception>();

            var tasks = Enumerable.Range(0, 32)
                                  .Select(_ => Task.Run(() =>
                                  {
                                      startGate.Wait();
                                      try
                                      {
                                          DuckType.EnableAotMode();
                                      }
                                      catch (Exception ex)
                                      {
                                          exceptions.Enqueue(ex);
                                      }
                                  }))
                                  .ToArray();

            startGate.Set();
            await Task.WhenAll(tasks);

            exceptions.Should().BeEmpty();
            DuckType.IsAotMode().Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentRuntimeInitializationRaceShouldKeepSingleMode()
        {
            var startGate = new ManualResetEventSlim(initialState: false);
            var exceptions = new ConcurrentQueue<Exception>();
            var dynamicCanCreateResults = new ConcurrentQueue<bool>();
            var tasks = new Task[32];

            for (var i = 0; i < 16; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    startGate.Wait();
                    try
                    {
                        DuckType.EnableAotMode();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                });
            }

            for (var i = 16; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    startGate.Wait();
                    try
                    {
                        var result = DuckType.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
                        dynamicCanCreateResults.Enqueue(result.CanCreate());
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                });
            }

            startGate.Set();
            await Task.WhenAll(tasks);

            var unexpectedExceptions = exceptions.Where(ex => ex is not DuckTypeRuntimeModeConflictException).ToList();
            unexpectedExceptions.Should().BeEmpty();

            if (DuckType.IsAotMode())
            {
                exceptions.Should().BeEmpty();
                dynamicCanCreateResults.Should().OnlyContain(canCreate => canCreate == false);
            }
            else
            {
                exceptions.Should().OnlyContain(ex => ex is DuckTypeRuntimeModeConflictException);
                exceptions.Should().NotBeEmpty();
                dynamicCanCreateResults.Should().OnlyContain(canCreate => canCreate);
            }
        }

        [Fact]
        public async Task ConcurrentDuplicateAotRegistrationsShouldRemainIdempotent()
        {
            var startGate = new ManualResetEventSlim(initialState: false);
            var exceptions = new ConcurrentQueue<Exception>();

            var tasks = Enumerable.Range(0, 32)
                                  .Select(_ => Task.Run(() =>
                                  {
                                      startGate.Wait();
                                      try
                                      {
                                          DuckTypeAotEngine.RegisterProxy(
                                              typeof(IDuplicateProxy),
                                              typeof(DuplicateTarget),
                                              typeof(DuplicateGeneratedProxy),
                                              instance => new DuplicateGeneratedProxy((DuplicateTarget)instance!));
                                      }
                                      catch (Exception ex)
                                      {
                                          exceptions.Enqueue(ex);
                                      }
                                  }))
                                  .ToArray();

            startGate.Set();
            await Task.WhenAll(tasks);

            exceptions.Should().BeEmpty();
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IDuplicateProxy), typeof(DuplicateTarget));
            result.CanCreate().Should().BeTrue();
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
        public void ForwardLookupRequiresExactMatchInAotMode()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(BaseForwardTarget),
                typeof(BaseForwardGeneratedProxy),
                instance => new BaseForwardGeneratedProxy((BaseForwardTarget)instance!));

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(DerivedForwardTarget));

            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void ForwardLookupDoesNotUseNullableFallbackInAotMode()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(IValueProxy),
                typeof(int?),
                typeof(ValueNullableGeneratedProxy),
                instance => new ValueNullableGeneratedProxy((int?)instance!));

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IValueProxy), typeof(int));

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

#if NET6_0_OR_GREATER
        [Fact]
        public void RegisterForwardProxyUsingMethodHandleAndResolve()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateForwardProxyWithMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                activatorMethod!.MethodHandle);

            var target = new ForwardTarget("hello");
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IForwardProxy>(target).Value.Should().Be("hello");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
            DuckTypeAotEngine.AdaptedTypedActivatorHandleCount.Should().Be(1);
        }

        [Fact]
        public void RegisterReverseProxyUsingMethodHandleAndResolve()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateReverseProxyWithMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                typeof(ReverseGeneratedProxy),
                activatorMethod!.MethodHandle);

            var result = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseProxy), typeof(ReverseTarget));
            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IReverseProxy>(new ReverseTarget("reverse")).Value.Should().Be("reverse");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
            DuckTypeAotEngine.AdaptedTypedActivatorHandleCount.Should().Be(1);
        }

        [Fact]
        public void RegisterForwardProxyUsingObjectBridgeMethodHandleAndResolve()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateForwardProxyWithObjectMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                activatorMethod!.MethodHandle);

            var target = new ForwardTarget("hello");
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IForwardProxy>(target).Value.Should().Be("hello");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(1);
            DuckTypeAotEngine.AdaptedTypedActivatorHandleCount.Should().Be(0);
        }

        [Fact]
        public void RegisterReverseProxyUsingObjectBridgeMethodHandleAndResolve()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateReverseProxyWithObjectMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                typeof(ReverseGeneratedProxy),
                activatorMethod!.MethodHandle);

            var result = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseProxy), typeof(ReverseTarget));
            result.CanCreate().Should().BeTrue();
            result.CreateInstance<IReverseProxy>(new ReverseTarget("reverse")).Value.Should().Be("reverse");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(1);
            DuckTypeAotEngine.AdaptedTypedActivatorHandleCount.Should().Be(0);
        }

        [Fact]
        public void RegisterForwardProxyUsingMethodHandleWithInvalidSignatureThrows()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateForwardProxyWithMethodHandleAndExtraParameter),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                activatorMethod!.MethodHandle);

            register.Should().Throw<ArgumentException>().WithMessage("*must declare exactly one parameter*");
        }

        [Fact]
        public void RegisterForwardProxyUsingMethodHandleWithIncompatibleReturnTypeThrows()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateSingleRegistryConflictProxyInstance),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                activatorMethod!.MethodHandle);

            register.Should().Throw<ArgumentException>().WithMessage("*return type*is not assignable*");
        }
#endif

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
        public void RegisterFailureUsingMethodHandleReplaysDeterministicFailure()
        {
            var throwerMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(ThrowKnownRegisteredFailure),
                BindingFlags.NonPublic | BindingFlags.Static);
            throwerMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                throwerMethod!.MethodHandle);

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotRegisteredFailureException>()
                        .WithMessage("*KnownDuckTypeFailure*missing-member*");
        }

        [Fact]
        public void RegisterFailureUsingMethodHandleDoesNotInvokeThrowerDuringBootstrap()
        {
            knownFailureThrowerInvocationCount = 0;
            var throwerMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(ThrowKnownRegisteredFailureWithCounter),
                BindingFlags.NonPublic | BindingFlags.Static);
            throwerMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                throwerMethod!.MethodHandle);

            knownFailureThrowerInvocationCount.Should().Be(0);

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotRegisteredFailureException>()
                        .WithMessage("*KnownDuckTypeFailure*missing-member*");
            knownFailureThrowerInvocationCount.Should().Be(1);
        }

        [Fact]
        public void RegisterFailureUsingMethodHandleReplaysKnownFailureTypeAndMessage()
        {
            const string expectedMessage = "The target method for the proxy method 'Void Missing()' was not found.";
            var throwerMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(ThrowKnownTargetMethodMissingFailure),
                BindingFlags.NonPublic | BindingFlags.Static);
            throwerMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                throwerMethod!.MethodHandle);

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeTargetMethodNotFoundException>()
                        .WithMessage(expectedMessage);
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

        [Fact]
        public void RegisterProxyFromDifferentRegistryAssemblyWithSameSimpleNameThrows()
        {
            DuckTypeAotEngine.RegisterProxy(
                typeof(ISingleRegistryWarmupProxy),
                typeof(SingleRegistryWarmupTarget),
                typeof(SingleRegistryWarmupGeneratedProxy),
                instance => new SingleRegistryWarmupGeneratedProxy((SingleRegistryWarmupTarget)instance!));

            var dynamicActivator = CreateSameSimpleNameDynamicAssemblyActivator();
            Action conflictingRegistryRegistration = () => DuckTypeAotEngine.RegisterProxy(
                typeof(ISingleRegistryConflictProxy),
                typeof(SingleRegistryConflictTarget),
                typeof(SingleRegistryConflictGeneratedProxy),
                dynamicActivator);

            conflictingRegistryRegistration.Should().Throw<DuckTypeAotMultipleRegistryAssembliesException>();
        }

        [Fact]
        public void ValidateContractWithSchemaMismatchThrows()
        {
            Action validate = () => DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract("999", CurrentDatadogTraceAssemblyVersion, CurrentDatadogTraceAssemblyMvid),
                CreateCurrentRegistryMetadata());

            validate.Should().Throw<DuckTypeAotRegistryContractValidationException>();
        }

        [Fact]
        public void ValidateContractWithDatadogTraceMismatchThrows()
        {
            Action validate = () => DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract(DuckTypeAotContract.CurrentSchemaVersion, "0.0.0.0", CurrentDatadogTraceAssemblyMvid),
                CreateCurrentRegistryMetadata());

            validate.Should().Throw<DuckTypeAotRegistryContractValidationException>();
        }

        [Fact]
        public void ValidateContractWithDifferentRegistryIdentityThenRegisterThrows()
        {
            DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract(
                    DuckTypeAotContract.CurrentSchemaVersion,
                    CurrentDatadogTraceAssemblyVersion,
                    CurrentDatadogTraceAssemblyMvid),
                new DuckTypeAotAssemblyMetadata(
                    "Fake.DuckType.Registry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    Guid.NewGuid().ToString("D")));

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                instance => new ForwardGeneratedProxy((ForwardTarget)instance!));

            register.Should().Throw<DuckTypeAotMultipleRegistryAssembliesException>();
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

        private class BaseForwardTarget
        {
            public BaseForwardTarget(string value)
            {
                Value = value;
            }

            public virtual string Value { get; }
        }

        private class DerivedForwardTarget : BaseForwardTarget
        {
            public DerivedForwardTarget(string value)
                : base(value)
            {
            }
        }

        private class BaseForwardGeneratedProxy : IForwardProxy
        {
            private readonly BaseForwardTarget _target;

            public BaseForwardGeneratedProxy(BaseForwardTarget target)
            {
                _target = target;
            }

            public string Value => _target.Value;
        }

        private interface IValueProxy
        {
            int Value { get; }
        }

        private class ValueNullableGeneratedProxy : IValueProxy
        {
            private readonly int? _value;

            public ValueNullableGeneratedProxy(int? value)
            {
                _value = value;
            }

            public int Value => _value ?? 0;
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

        private static IForwardProxy CreateForwardProxyWithMethodHandle(ForwardTarget instance)
        {
            return new ForwardGeneratedProxy(instance);
        }

        private static IForwardProxy CreateForwardProxyWithObjectMethodHandle(object? instance)
        {
            return new ForwardGeneratedProxy((ForwardTarget)instance!);
        }

        private static IForwardProxy CreateForwardProxyWithMethodHandleAndExtraParameter(object? instance, int ignored)
        {
            return new ForwardGeneratedProxy((ForwardTarget)instance!);
        }

        private static IReverseProxy CreateReverseProxyWithMethodHandle(ReverseTarget instance)
        {
            return new ReverseGeneratedProxy(instance);
        }

        private static IReverseProxy CreateReverseProxyWithObjectMethodHandle(object? instance)
        {
            return new ReverseGeneratedProxy((ReverseTarget)instance!);
        }

        private static object CreateSingleRegistryConflictProxyInstance(object? instance)
        {
            return new SingleRegistryConflictGeneratedProxy((SingleRegistryConflictTarget)instance!);
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

        private static Func<object?, object?> CreateSameSimpleNameDynamicAssemblyActivator()
        {
            var currentAssemblySimpleName = typeof(DuckTypeAotEngineTests).Assembly.GetName().Name;
            currentAssemblySimpleName.Should().NotBeNullOrEmpty();

            var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(currentAssemblySimpleName!), AssemblyBuilderAccess.Run);
            var dynamicModule = dynamicAssembly.DefineDynamicModule("MainModule");
            var dynamicType = dynamicModule.DefineType(
                "SingleRegistrySameNameActivatorFactory",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var dynamicMethod = dynamicType.DefineMethod(
                "Create",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(object),
                [typeof(object)]);

            var bridgeMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateSingleRegistryConflictProxyInstance),
                BindingFlags.NonPublic | BindingFlags.Static);
            bridgeMethod.Should().NotBeNull();

            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, bridgeMethod!);
            il.Emit(OpCodes.Ret);

            var factoryType = dynamicType.CreateTypeInfo();
            factoryType.Should().NotBeNull();

            var createMethod = factoryType!.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            createMethod.Should().NotBeNull();

            return (Func<object?, object?>)Delegate.CreateDelegate(typeof(Func<object?, object?>), createMethod!);
        }

        private static string CurrentDatadogTraceAssemblyVersion => typeof(DuckTypeAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        private static string CurrentDatadogTraceAssemblyMvid => typeof(DuckTypeAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        private static int knownFailureThrowerInvocationCount;

        private static DuckTypeAotAssemblyMetadata CreateCurrentRegistryMetadata()
        {
            var assembly = typeof(DuckTypeAotEngineTests).Assembly;
            return new DuckTypeAotAssemblyMetadata(
                assembly.FullName ?? assembly.GetName().Name ?? "unknown",
                assembly.ManifestModule.ModuleVersionId.ToString("D"));
        }

        private static void ThrowKnownRegisteredFailure()
        {
            DuckTypeAotRegisteredFailureException.Throw("KnownDuckTypeFailure", "missing-member");
        }

        private static void ThrowKnownRegisteredFailureWithCounter()
        {
            Interlocked.Increment(ref knownFailureThrowerInvocationCount);
            ThrowKnownRegisteredFailure();
        }

        private static void ThrowKnownTargetMethodMissingFailure()
        {
            DuckTypeAotRegisteredFailureException.Throw(
                typeof(DuckTypeTargetMethodNotFoundException).FullName!,
                "The target method for the proxy method 'Void Missing()' was not found.");
        }
    }
}
