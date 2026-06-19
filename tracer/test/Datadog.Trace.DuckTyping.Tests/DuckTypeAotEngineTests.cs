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
            AssertFailureResultHasNoActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
            Action createProxy = () => _ = result.CreateInstance<IMissingProxy>(new MissingTarget());
            createProxy.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
            DuckType.EnableAotMode();
            Action createViaDuckType = () => _ = DuckType.Create(typeof(IMissingProxy), new MissingTarget());
            createViaDuckType.Should().Throw<TargetInvocationException>()
                             .WithInnerException<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void MissingReverseMappingReturnsErrorResult()
        {
            var result = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseProxy), typeof(ReverseTarget));

            result.CanCreate().Should().BeFalse();
            AssertFailureResultHasNoActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
            Action createProxy = () => _ = result.CreateInstance<IReverseProxy>(new ReverseTarget("missing"));
            createProxy.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
            DuckType.EnableAotMode();
            Action createViaDuckType = () => _ = DuckType.CreateReverse(typeof(IReverseProxy), new ReverseTarget("missing"));
            createViaDuckType.Should().Throw<TargetInvocationException>()
                             .WithInnerException<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void DynamicNonGenericFailureKeepsTargetInvocationExceptionContractWithoutActivator()
        {
            var result = DuckType.GetOrCreateProxyType(typeof(IDynamicFailureProxy), typeof(DynamicFailureTarget));

            result.CanCreate().Should().BeFalse();
            AssertFailureResultHasNoActivator(result);
            Action createGeneric = () => _ = result.CreateInstance<IDynamicFailureProxy>(new DynamicFailureTarget());
            createGeneric.Should().Throw<DuckTypeProxyAndTargetMethodReturnTypeMismatchException>();
            Action createNonGeneric = () => _ = DuckType.Create(typeof(IDynamicFailureProxy), new DynamicFailureTarget());
            createNonGeneric.Should().Throw<TargetInvocationException>()
                            .WithInnerException<DuckTypeProxyAndTargetMethodReturnTypeMismatchException>();
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
        public void RegisterForwardProxyUsingTypedMethodHandleThrows()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateForwardProxyWithMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                activatorMethod!.MethodHandle);

            register.Should()
                    .Throw<ArgumentException>()
                    .WithMessage("*must declare exactly one parameter of type 'object'*Typed method-handle activators are not supported*");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
        }

        [Fact]
        public void RegisterReverseProxyUsingTypedMethodHandleThrows()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateReverseProxyWithMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            Action register = () => DuckTypeAotEngine.RegisterReverseProxy(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                typeof(ReverseGeneratedProxy),
                activatorMethod!.MethodHandle);

            register.Should()
                    .Throw<ArgumentException>()
                    .WithMessage("*must declare exactly one parameter of type 'object'*Typed method-handle activators are not supported*");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
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
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            result.CreateInstance<IForwardProxy>(target).Value.Should().Be("hello");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(1);

            var activator = GetCreateTypeResultField<Delegate>(result, "_activator");
            var untypedActivator = GetCreateTypeResultField<Func<object?, object?>>(result, "_untypedActivator");
            activator.Should().NotBeNull();
            activator!.GetType().Should().Be(typeof(CreateProxyInstance<IForwardProxy>));
            untypedActivator.Should().NotBeNull();
            untypedActivator!.Method.Name.Should().Be(activator.Method.Name);
            untypedActivator.Method.DeclaringType.Should().Be(activator.Method.DeclaringType);
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
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            result.CreateInstance<IReverseProxy>(new ReverseTarget("reverse")).Value.Should().Be("reverse");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(1);

            var activator = GetCreateTypeResultField<Delegate>(result, "_activator");
            var untypedActivator = GetCreateTypeResultField<Func<object?, object?>>(result, "_untypedActivator");
            activator.Should().NotBeNull();
            activator!.GetType().Should().Be(typeof(CreateProxyInstance<IReverseProxy>));
            untypedActivator.Should().NotBeNull();
            untypedActivator!.Method.Name.Should().Be(activator.Method.Name);
            untypedActivator.Method.DeclaringType.Should().Be(activator.Method.DeclaringType);
        }

        [Fact]
        public void RegisterValueTypeProxyUsingObjectBridgeMethodHandleThrows()
        {
            var activatorMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(CreateValueTypeProxyWithObjectMethodHandle),
                BindingFlags.NonPublic | BindingFlags.Static);
            activatorMethod.Should().NotBeNull();

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(ValueTypeDuckCopyProxy),
                typeof(ValueTypeTarget),
                typeof(ValueTypeDuckCopyProxy),
                activatorMethod!.MethodHandle);

            register.Should()
                    .Throw<ArgumentException>()
                    .WithMessage("*RuntimeMethodHandle activator methods are not supported for value-type proxy definition*Register a direct Func<object?, object?> delegate instead*");
            DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
        }

        [Fact]
        public void GenericForwardAndReverseFastPathsShouldNotShareCachedResultForSameTypePair()
        {
            DuckType.RegisterAotProxy(
                typeof(ISharedForwardReverseProxy),
                typeof(SharedForwardReverseTarget),
                typeof(SharedForwardGeneratedProxy),
                instance => new SharedForwardGeneratedProxy((SharedForwardReverseTarget)instance!));
            DuckType.RegisterAotReverseProxy(
                typeof(ISharedForwardReverseProxy),
                typeof(SharedForwardReverseTarget),
                typeof(SharedReverseGeneratedProxy),
                instance => new SharedReverseGeneratedProxy((SharedForwardReverseTarget)instance!));

            var target = new SharedForwardReverseTarget("cache");

            DuckType.CreateCache<ISharedForwardReverseProxy>.Create(target)!.Value.Should().Be("forward:cache");
            DuckType.CreateCache<ISharedForwardReverseProxy>.CreateReverse(target)!.Value.Should().Be("reverse:cache");
            DuckType.CreateCache<ISharedForwardReverseProxy>.Create(target)!.Value.Should().Be("forward:cache");
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
        public void LateRegistrationInvalidatesGenericFastPathMiss()
        {
            DuckType.EnableAotMode();
            var missingResult = DuckType.CreateCache<ILateProxy>.GetProxy(typeof(LateTarget));
            missingResult.CanCreate().Should().BeFalse();

            DuckType.RegisterAotProxy(
                typeof(ILateProxy),
                typeof(LateTarget),
                typeof(LateGeneratedProxy),
                instance => new LateGeneratedProxy((LateTarget)instance!));

            DuckType.CreateCache<ILateProxy>.Create(new LateTarget(42))!.Number.Should().Be(42);
        }

        [Fact]
        public void ResetRuntimeModeForTestsInvalidatesGenericAotFastPathBeforeDynamicReuse()
        {
            DuckType.RegisterAotProxy(
                typeof(IResetProxy),
                typeof(ResetTarget),
                typeof(ResetGeneratedProxy),
                instance => new ResetGeneratedProxy((ResetTarget)instance!));
            DuckType.Create<IResetProxy>(new ResetTarget("value"))!.Value.Should().Be("aot:value");

            DuckType.ResetRuntimeModeForTests();

            DuckType.Create<IResetProxy>(new ResetTarget("value"))!.Value.Should().Be("value");
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
            AssertFailureResultHasNoActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotRegisteredFailureException>()
                        .WithMessage("*KnownDuckTypeFailure*missing-member*");
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<DuckTypeAotRegisteredFailureException>()
                       .WithMessage("*KnownDuckTypeFailure*missing-member*");
            DuckType.EnableAotMode();
            Action createViaDuckType = () => _ = DuckType.Create(typeof(IForwardProxy), new ForwardTarget("failure"));
            createViaDuckType.Should().Throw<TargetInvocationException>()
                            .WithInnerException<DuckTypeAotRegisteredFailureException>();
        }

        [Fact]
        public void RegisterReverseFailureUsingMethodHandleReplaysDeterministicFailureWithoutActivator()
        {
            var throwerMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(ThrowKnownRegisteredFailure),
                BindingFlags.NonPublic | BindingFlags.Static);
            throwerMethod.Should().NotBeNull();

            DuckTypeAotEngine.RegisterReverseProxyFailure(
                typeof(IReverseProxy),
                typeof(ReverseTarget),
                throwerMethod!.MethodHandle);

            var result = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IReverseProxy), typeof(ReverseTarget));

            result.CanCreate().Should().BeFalse();
            AssertFailureResultHasNoActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeAotRegisteredFailureException>()
                        .WithMessage("*KnownDuckTypeFailure*missing-member*");
            Action createProxy = () => _ = result.CreateInstance<IReverseProxy>(new ReverseTarget("failure"));
            createProxy.Should().Throw<DuckTypeAotRegisteredFailureException>()
                       .WithMessage("*KnownDuckTypeFailure*missing-member*");
            DuckType.EnableAotMode();
            Action createViaDuckType = () => _ = DuckType.CreateReverse(typeof(IReverseProxy), new ReverseTarget("failure"));
            createViaDuckType.Should().Throw<TargetInvocationException>()
                            .WithInnerException<DuckTypeAotRegisteredFailureException>();
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
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<DuckTypeAotRegisteredFailureException>()
                       .WithMessage("*KnownDuckTypeFailure*missing-member*");
            knownFailureThrowerInvocationCount.Should().Be(2);
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
            AssertFailureResultHasNoActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeTargetMethodNotFoundException>()
                        .WithMessage(expectedMessage);
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<DuckTypeTargetMethodNotFoundException>()
                       .WithMessage(expectedMessage);
        }

        [Fact]
        public void RegisterFailureUsingExceptionTypeDoesNotDefineRegistryAssemblyIdentity()
        {
            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IMissingProxy),
                typeof(MissingTarget),
                typeof(DuckTypePropertyCantBeWrittenException));

            Action register = () => DuckTypeAotEngine.RegisterProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                instance => new ForwardGeneratedProxy((ForwardTarget)instance!));

            register.Should().NotThrow();
            DuckType.EnableAotMode();
            DuckType.Create<IForwardProxy>(new ForwardTarget("value"))!.Value.Should().Be("value");
        }

        [Fact]
        public void RegisterProxyFailureFromDifferentRegistryAssemblyThrows()
        {
            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                (Action)ThrowKnownRegisteredFailure);

            var dynamicThrower = CreateSameSimpleNameDynamicAssemblyFailureThrower();
            Action conflictingRegistryRegistration = () => DuckTypeAotEngine.RegisterProxyFailure(
                typeof(ISingleRegistryConflictProxy),
                typeof(SingleRegistryConflictTarget),
                dynamicThrower);

            conflictingRegistryRegistration.Should().Throw<DuckTypeAotMultipleRegistryAssembliesException>();
        }

        [Fact]
        public async Task RegisterProxyFailureValidatesRegistryIdentityInsideRegistrationLock()
        {
            var registrationLock = GetDuckTypeAotRegistrationLock();
            var registeredIdentityField = GetDuckTypeAotEngineStaticField("_registeredRegistryAssemblyIdentity");
            using var started = new ManualResetEventSlim(initialState: false);

            var registrationTask = Task.CompletedTask;
            Monitor.Enter(registrationLock);
            try
            {
                registrationTask = Task.Run(() =>
                {
                    started.Set();
                    DuckTypeAotEngine.RegisterProxyFailure(
                        typeof(IForwardProxy),
                        typeof(ForwardTarget),
                        (Action)ThrowKnownRegisteredFailure);
                });

                started.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                SpinWait.SpinUntil(
                    () => registeredIdentityField.GetValue(null) is not null || registrationTask.IsCompleted,
                    TimeSpan.FromMilliseconds(100));

                registeredIdentityField.SetValue(
                    null,
                    $"Fake.DuckType.Registry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null; MVID={Guid.NewGuid():D}");
            }
            finally
            {
                Monitor.Exit(registrationLock);
            }

            Func<Task> waitForRegistration = async () => await registrationTask;
            await waitForRegistration.Should().ThrowAsync<DuckTypeAotMultipleRegistryAssembliesException>();
        }

        [Fact]
        public void ValidateContractWithDifferentRegistryIdentityThenRegisterFailureThrows()
        {
            DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract(
                    DuckTypeAotContract.CurrentSchemaVersion,
                    CurrentDatadogTraceAssemblyVersion,
                    CurrentDatadogTraceAssemblyMvid),
                new DuckTypeAotAssemblyMetadata(
                    "Fake.DuckType.Registry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    Guid.NewGuid().ToString("D")));

            Action register = () => DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                (Action)ThrowKnownRegisteredFailure);

            register.Should().Throw<DuckTypeAotMultipleRegistryAssembliesException>();
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

        private interface IDynamicFailureProxy
        {
            string GetValue();
        }

        private class DynamicFailureTarget
        {
            public void GetValue()
            {
            }
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

        private interface IResetProxy
        {
            string Value { get; }
        }

        private class ResetTarget
        {
            public ResetTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ResetGeneratedProxy : IResetProxy
        {
            private readonly ResetTarget _target;

            public ResetGeneratedProxy(ResetTarget target)
            {
                _target = target;
            }

            public string Value => "aot:" + _target.Value;
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

        private interface ISharedForwardReverseProxy
        {
            string Value { get; }
        }

        private class SharedForwardReverseTarget
        {
            public SharedForwardReverseTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class SharedForwardGeneratedProxy : ISharedForwardReverseProxy
        {
            private readonly SharedForwardReverseTarget _target;

            public SharedForwardGeneratedProxy(SharedForwardReverseTarget target)
            {
                _target = target;
            }

            public string Value => "forward:" + _target.Value;
        }

        private class SharedReverseGeneratedProxy : ISharedForwardReverseProxy
        {
            private readonly SharedForwardReverseTarget _target;

            public SharedReverseGeneratedProxy(SharedForwardReverseTarget target)
            {
                _target = target;
            }

            public string Value => "reverse:" + _target.Value;
        }

        [DuckCopy]
        private struct ValueTypeDuckCopyProxy
        {
            public string Value;
        }

        private class ValueTypeTarget
        {
            public ValueTypeTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
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

        private static ValueTypeDuckCopyProxy CreateValueTypeProxyWithObjectMethodHandle(object? instance)
        {
            return new ValueTypeDuckCopyProxy { Value = ((ValueTypeTarget)instance!).Value };
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

        private static Action CreateSameSimpleNameDynamicAssemblyFailureThrower()
        {
            var currentAssemblySimpleName = typeof(DuckTypeAotEngineTests).Assembly.GetName().Name;
            currentAssemblySimpleName.Should().NotBeNullOrEmpty();

            var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(currentAssemblySimpleName!), AssemblyBuilderAccess.Run);
            var dynamicModule = dynamicAssembly.DefineDynamicModule("MainModule");
            var dynamicType = dynamicModule.DefineType(
                "SingleRegistrySameNameFailureThrowerFactory",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var dynamicMethod = dynamicType.DefineMethod(
                "Throw",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(void),
                Type.EmptyTypes);

            var bridgeMethod = typeof(DuckTypeAotEngineTests).GetMethod(
                nameof(ThrowKnownRegisteredFailure),
                BindingFlags.NonPublic | BindingFlags.Static);
            bridgeMethod.Should().NotBeNull();

            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Call, bridgeMethod!);
            il.Emit(OpCodes.Ret);

            var factoryType = dynamicType.CreateTypeInfo();
            factoryType.Should().NotBeNull();

            var throwMethod = factoryType!.GetMethod("Throw", BindingFlags.Public | BindingFlags.Static);
            throwMethod.Should().NotBeNull();

            return (Action)Delegate.CreateDelegate(typeof(Action), throwMethod!);
        }

        private static TField? GetCreateTypeResultField<TField>(DuckType.CreateTypeResult result, string fieldName)
        {
            var field = typeof(DuckType.CreateTypeResult).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (TField?)field!.GetValue(result);
        }

        private static object GetDuckTypeAotRegistrationLock()
        {
            var registrationLock = GetDuckTypeAotEngineStaticField("RegistrationLock").GetValue(null);
            registrationLock.Should().NotBeNull();
            return registrationLock!;
        }

        private static FieldInfo GetDuckTypeAotEngineStaticField(string fieldName)
        {
            var field = typeof(DuckTypeAotEngine).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field!;
        }

        private static void AssertFailureResultHasNoActivator(DuckType.CreateTypeResult result)
        {
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            GetCreateTypeResultField<Delegate>(result, "_activator").Should().BeNull();
            GetCreateTypeResultField<Func<object?, object?>>(result, "_untypedActivator").Should().BeNull();
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
