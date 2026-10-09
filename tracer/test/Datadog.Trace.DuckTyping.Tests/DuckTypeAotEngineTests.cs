// <copyright file="DuckTypeAotEngineTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
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
            DuckType.RuntimeMode.Should().Be(DuckTypeRuntimeMode.Dynamic);
        }

        [Fact]
        public void RuntimeModeShouldBeImmutableAfterAotInitialization()
        {
            DuckType.EnableAotMode();
            DuckType.RuntimeMode.Should().Be(DuckTypeRuntimeMode.Aot);

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
            DuckType.RuntimeMode.Should().Be(DuckTypeRuntimeMode.Aot);
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

            if (DuckType.RuntimeMode == DuckTypeRuntimeMode.Aot)
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
            AssertFailureResultHasNoProxyActivator(result);
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
            AssertFailureResultHasNoProxyActivator(result);
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
        public void LookingUpAnUnregisteredPairShouldNotRaiseFirstChanceExceptions()
        {
            // Like dynamic failures, the missing registration exception is only thrown when a proxy is created: probing an
            // unmapped pair (CanCreate, TryDuckCast...) must not show up as first-chance exceptions (e.g. in runtime metrics).
            DuckType.EnableAotMode();
            var testThreadId = Environment.CurrentManagedThreadId;
            var firstChanceExceptions = 0;
            void CountFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
            {
                if (args.Exception is DuckTypeAotMissingProxyRegistrationException && Environment.CurrentManagedThreadId == testThreadId)
                {
                    Interlocked.Increment(ref firstChanceExceptions);
                }
            }

            AppDomain.CurrentDomain.FirstChanceException += CountFirstChanceException;
            try
            {
                DuckType.CanCreate<IMissingProxy>(new MissingTarget()).Should().BeFalse();
                new MissingTarget().TryDuckCast<IMissingProxy>(out _).Should().BeFalse();
                DuckType.GetOrCreateProxyType(typeof(IMissingProxy), typeof(MissingTarget)).CanCreate().Should().BeFalse();
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= CountFirstChanceException;
            }

            firstChanceExceptions.Should().Be(0);
            Action create = () => DuckType.Create<IMissingProxy>(new MissingTarget());
            create.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
        }

        [Fact]
        public void ObjectBasedCreationShouldWrapActivatorExceptionsLikeDynamicDuckTyping()
        {
            // Dynamic duck typing creates object-based proxies through DynamicInvoke, which wraps whatever the activator
            // throws in a TargetInvocationException; the generic APIs call the activator directly in both modes.
            DuckType.EnableAotMode();
            DuckType.RegisterAotProxy(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                typeof(ForwardGeneratedProxy),
                (Func<object?, object?>)(_ => throw new InvalidOperationException("boom")));

            Action createNonGeneric = () => DuckType.Create(typeof(IForwardProxy), new ForwardTarget("value"));
            createNonGeneric.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();
            Action createGeneric = () => DuckType.Create<IForwardProxy>(new ForwardTarget("value"));
            createGeneric.Should().Throw<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public void DynamicNonGenericFailureKeepsTargetInvocationExceptionContractWithoutActivator()
        {
            var result = DuckType.GetOrCreateProxyType(typeof(IDynamicFailureProxy), typeof(DynamicFailureTarget));

            result.CanCreate().Should().BeFalse();
            AssertFailureResultHasNoProxyActivator(result);
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

            // The method-handle activator is rebound once to an object-callable delegate over the same generated method.
            var activator = GetGeneratedObjectActivator(result);
            activator.Should().NotBeNull();
            activator!.GetType().Should().Be(typeof(Func<object?, object?>));
            activator.Method.Name.Should().Be(activatorMethod.Name);
            activator.Method.DeclaringType.Should().Be(activatorMethod.DeclaringType);
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

            // The method-handle activator is rebound once to an object-callable delegate over the same generated method.
            var activator = GetGeneratedObjectActivator(result);
            activator.Should().NotBeNull();
            activator!.GetType().Should().Be(typeof(Func<object?, object?>));
            activator.Method.Name.Should().Be(activatorMethod.Name);
            activator.Method.DeclaringType.Should().Be(activatorMethod.DeclaringType);
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

#if NETCOREAPP
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void WarmMultiTargetCacheLookupsShouldNotAllocate(bool reverse, bool generic, bool aot)
        {
            var firstTarget = typeof(SharedForwardReverseTarget);
            var secondTarget = typeof(SecondSharedForwardReverseTarget);
            foreach (var targetType in aot ? new[] { firstTarget, secondTarget } : Array.Empty<Type>())
            {
                if (reverse)
                {
                    DuckType.RegisterAotReverseProxy(
                        typeof(ISharedForwardReverseProxy),
                        targetType,
                        typeof(SharedReverseGeneratedProxy),
                        instance => new SharedReverseGeneratedProxy((SharedForwardReverseTarget)instance!));
                }
                else
                {
                    DuckType.RegisterAotProxy(
                        typeof(ISharedForwardReverseProxy),
                        targetType,
                        typeof(SharedForwardGeneratedProxy),
                        instance => new SharedForwardGeneratedProxy((SharedForwardReverseTarget)instance!));
                }
            }

            var created = 0;
            for (var i = 0; i < 10_000; i++)
            {
                created += Lookup((i & 1) == 0 ? firstTarget : secondTarget).CanCreate() ? 1 : 0;
            }

            created.Should().Be(10_000);

            // A background GC still running (after the allocations of the previous tests) retires the allocation context of
            // this thread when it suspends the runtime: its unused part would count as allocated.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++)
            {
                _ = Lookup((i & 1) == 0 ? firstTarget : secondTarget);
            }

            (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore).Should().Be(0);

            DuckType.CreateTypeResult Lookup(Type targetType) => generic
                ? reverse ? DuckType.CreateCache<ISharedForwardReverseProxy>.GetReverseProxy(targetType) : DuckType.CreateCache<ISharedForwardReverseProxy>.GetProxy(targetType)
                : reverse ? DuckType.GetOrCreateReverseProxyType(typeof(ISharedForwardReverseProxy), targetType) : DuckType.GetOrCreateProxyType(typeof(ISharedForwardReverseProxy), targetType);
        }
#endif

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
            AssertFailureResultHasNoProxyActivator(result);
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
            AssertFailureResultHasNoProxyActivator(result);
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
        public void RegisterFailureUsingAThrowerThatDoesNotThrowStillFails()
        {
            // A legacy failure registration is a thrower: one that returns must not make the failed result look usable.
            DuckTypeAotEngine.RegisterProxyFailure(typeof(IForwardProxy), typeof(ForwardTarget), () => { });

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeFalse();
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<DuckTypeException>().WithMessage("*didn't throw*");
        }

        [Fact]
        public void ArrayProxyRegistrationServesEveryArrayType()
        {
            // One generated proxy per array mapping: it stores the instance as System.Array, and reports the looked-up type.
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(IArrayLengthProxy),
                typeof(object[]),
                typeof(ArrayLengthGeneratedProxy),
                (instance, arrayType) => new ArrayLengthGeneratedProxy((Array)instance!, arrayType));

            foreach (var array in new Array[] { new object[1], new string[2], new int[3][], new int[4] })
            {
                var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IArrayLengthProxy), array.GetType());
                result.CanCreate().Should().BeTrue();
                var proxy = result.CreateInstance<IArrayLengthProxy>(array);
                proxy.Length.Should().Be(array.Length);
                ((IDuckType)proxy).Type.Should().Be(array.GetType());
            }

            DuckTypeAotEngine.GetOrCreateProxyType(typeof(IArrayLengthProxy), typeof(ForwardTarget)).CanCreate().Should().BeFalse();
        }

        [Fact]
        public void FallbackProxyRegistrationServesNonPublicTypesOfTheCoreLibrary()
        {
            // A registry can't name System.RuntimeType: the registration of System.Type serves it, and reports it as IDuckType.Type.
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(Type),
                typeof(TypeNameGeneratedProxy),
                (instance, type) => new TypeNameGeneratedProxy((Type)instance!, type));

            var runtimeType = typeof(string).GetType();
            runtimeType.IsVisible.Should().BeFalse();
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), runtimeType);
            result.CanCreate().Should().BeTrue();
            var proxy = result.CreateInstance<ITypeNameProxy>(typeof(string));
            proxy.Name.Should().Be("String");
            ((IDuckType)proxy).Type.Should().Be(runtimeType);

            // Like the activator dynamic duck typing creates for the looked-up type, which casts the instance to it.
            Action createFromAnotherType = () => result.CreateInstance<ITypeNameProxy>(new object());
            createFromAnotherType.Should().Throw<InvalidCastException>().WithMessage($"Unable to cast object of type 'System.Object' to type '{runtimeType}'.");

            // A public type of the core library is one a registry names: it has its own registration, or none.
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(TypeDelegator)).CanCreate().Should().BeFalse();
        }

        [Fact]
        public void FallbackFailureRegistrationNamesTheLookedUpType()
        {
            // The failure registered for int[] is thrown for long[] with the name dynamic duck typing gives it for long[].
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(IArrayLengthProxy),
                typeof(int[]),
                () => DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypePropertyOrFieldNotFoundException).FullName!, "The property or field 'Missing' was not found in the instance of type 'System.Int32[]'."));

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IArrayLengthProxy), typeof(long[]));
            result.CanCreate().Should().BeFalse();
            Action create = () => result.CreateInstance<IArrayLengthProxy>(new long[1]);
            create.Should().Throw<DuckTypePropertyOrFieldNotFoundException>().WithMessage("The property or field 'Missing' was not found in the instance of type 'System.Int64[]'.");
        }

        [Fact]
        public void FallbackFailureOfAnotherArrayTypeKeepsTheWrappedException()
        {
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(IArrayLengthProxy),
                typeof(int[]),
                () => DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypeException).FullName!, "Error creating duck type for type: 'System.Int32[]'", typeof(TypeLoadException).FullName!, "Method 'get_Length' does not have an implementation."));

            Action create = () => DuckTypeAotEngine.GetOrCreateProxyType(typeof(IArrayLengthProxy), typeof(long[])).CreateInstance<IArrayLengthProxy>(new long[1]);
            create.Should().Throw<DuckTypeException>()
                  .WithMessage("Error creating duck type for type: 'System.Int64[]'")
                  .WithInnerException<TypeLoadException>()
                  .WithMessage("Method 'get_Length' does not have an implementation.");
        }

        [Fact]
        public void FallbackDoesNotServeTheFailureOfACoreLibraryTypeToTheTypesDerivingFromIt()
        {
            // System.RuntimeType can have the members System.Type misses: without a proxy, it has no registration (like any type
            // the registry doesn't register), named in the exception.
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(ITypeNameProxy),
                typeof(Type),
                () => DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypePropertyOrFieldNotFoundException).FullName!, "The property or field 'Name' was not found in the instance of type 'System.Type'."));

            var runtimeType = typeof(string).GetType();
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), runtimeType);
            result.CanCreate().Should().BeFalse();
            Action create = () => result.CreateInstance<ITypeNameProxy>(typeof(string));
            create.Should().Throw<DuckTypeAotMissingProxyRegistrationException>().WithMessage($"*'{runtimeType.FullName}'*");
        }

        [Fact]
        public void FallbackServesTheMostDerivedCoreLibraryProxyAndIgnoresFailures()
        {
            // The enumerator of an array implements IEnumerator<int> (whose mapping fails) and IDisposable (whose proxy serves it).
            var enumeratorType = ((IEnumerable<int>)new[] { 1 }).GetEnumerator().GetType();
            enumeratorType.IsVisible.Should().BeFalse();
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(ITypeNameProxy),
                typeof(IEnumerator<int>),
                () => DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypePropertyOrFieldNotFoundException).FullName!, "The property or field 'Name' was not found."));
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(IDisposable),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type));
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(object),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "object"));

            var proxy = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), enumeratorType).CreateInstance<ITypeNameProxy>(((IEnumerable<int>)new[] { 1 }).GetEnumerator());
            proxy.Name.Should().Be("disposable");
            ((IDuckType)proxy).Type.Should().Be(enumeratorType);
        }

        [Fact]
        public void FallbackServesClosedGenericCoreLibraryTypesOverNonPublicCoreLibraryTypes()
        {
            // Task<VoidTaskResult> (Task.CompletedTask, the tasks of async methods) is built on a non-public type: the proxy of
            // Task serves it when the registry doesn't register it.
            var voidTaskResultTaskType = typeof(Task<>).MakeGenericType(typeof(Task).Assembly.GetType("System.Threading.Tasks.VoidTaskResult", throwOnError: true)!);
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(Task),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "task"));

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), voidTaskResultTaskType);
            result.CanCreate().Should().BeTrue();
            var proxy = result.CreateInstance<ITypeNameProxy>(Activator.CreateInstance(voidTaskResultTaskType, nonPublic: true)!);
            proxy.Name.Should().Be("task");
            ((IDuckType)proxy).Type.Should().Be(voidTaskResultTaskType);
        }

        [Fact]
        public async Task FallbackServesTheClosestRegisteredBaseClassBeforeInterfaces()
        {
            // The box of an async method derives from Task<VoidTaskResult>, which derives from Task.
            var box = DelayedAsync();
            var boxType = box.GetType();
            var voidTaskResultTaskType = typeof(Task<>).MakeGenericType(typeof(Task).Assembly.GetType("System.Threading.Tasks.VoidTaskResult", throwOnError: true)!);
            boxType.BaseType.Should().Be(voidTaskResultTaskType);
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(IAsyncResult),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "async-result"));
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(Task),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "task"));

            // A registered base class wins over an interface.
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), boxType).CreateInstance<ITypeNameProxy>(box).Name.Should().Be("task");

            // The failure of a closer base class: the box inherits the members that fail it, so it isn't served by another proxy.
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(ITypeNameProxy),
                voidTaskResultTaskType,
                () => DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypeProxyAndTargetMethodReturnTypeMismatchException).FullName!, "Return type mismatch."));
            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), boxType);
            result.CanCreate().Should().BeFalse();
            Action create = () => result.CreateInstance<ITypeNameProxy>(box);
            create.Should().Throw<DuckTypeAotMissingProxyRegistrationException>();
            await box;

            static async Task DelayedAsync() => await Task.Delay(1).ConfigureAwait(false);
        }

        [Fact]
        public void FallbackDoesNotServeTypesARegistryCanName()
        {
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(object),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "object"));
            DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(ValueType),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type, "value-type"));

            // A public generic type of the core library closed over a type of another assembly, even a non-public one.
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(List<ForwardTarget>)).CanCreate().Should().BeFalse();

            // A non-public value type of the core library: a proxy of a type it derives from would share the boxed instance,
            // where dynamic duck typing's copies it.
            var voidTaskResultType = typeof(Task).Assembly.GetType("System.Threading.Tasks.VoidTaskResult", throwOnError: true)!;
            voidTaskResultType.IsValueType.Should().BeTrue();
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), voidTaskResultType).CanCreate().Should().BeFalse();
        }

        [Fact]
        public void FallbackProxyRegistrationRequiresAnArrayOrACoreLibraryTargetType()
        {
            Action register = () => DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                typeof(RecordedContract),
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type));
            register.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DerivedTypesProxyRegistrationServesTheClassesDerivingFromItsTargetType()
        {
            // [DuckType(IncludeDerivedTypes = true)]: the registry can't name the classes deriving from the target type.
            RegisterDerivedTypesProxy(typeof(DerivedTypesBase), "base");
            RegisterDerivedTypesProxy(typeof(IDerivedTypesContract), "contract");

            // A registered base class wins over an interface, and the proxy reports the looked-up type.
            var proxy = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(DerivedTypesGrandChild)).CreateInstance<ITypeNameProxy>(new DerivedTypesGrandChild());
            proxy.Name.Should().Be("base");
            ((IDuckType)proxy).Type.Should().Be(typeof(DerivedTypesGrandChild));
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(DerivedTypesContractImplementation)).CreateInstance<ITypeNameProxy>(new DerivedTypesContractImplementation()).Name.Should().Be("contract");

            // A closer base class registered later serves the classes deriving from it.
            RegisterDerivedTypesProxy(typeof(DerivedTypesChild), "child");
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(DerivedTypesGrandChild)).CreateInstance<ITypeNameProxy>(new DerivedTypesGrandChild()).Name.Should().Be("child");

            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(ForwardTarget)).CanCreate().Should().BeFalse();

            static void RegisterDerivedTypesProxy(Type targetType, string name)
                => DuckTypeAotEngine.RegisterFallbackProxy(
                    typeof(ITypeNameProxy),
                    targetType,
                    typeof(DisposableNameGeneratedProxy),
                    (instance, type) => new DisposableNameGeneratedProxy(instance!, type, name),
                    derivedTypes: true);
        }

        [Fact]
        public void GenericProxyRegistrationServesTheInstantiationsOfItsTargetType()
        {
            // The registry generates the proxy generic over the type parameters of the target: the activator of an instantiation
            // is created with its type arguments.
            DuckTypeAotEngine.RegisterGenericProxy(
                typeof(ITypeNameProxy),
                typeof(GenericProxyTarget<>),
                derivedTypes: true,
                arguments => Activator.CreateInstance(typeof(GenericProxyActivator<>).MakeGenericType(arguments))!);

            var proxy = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(GenericProxyTarget<ForwardTarget>)).CreateInstance<ITypeNameProxy>(new GenericProxyTarget<ForwardTarget>());
            proxy.Name.Should().Be(nameof(ForwardTarget));
            ((IDuckType)proxy).Type.Should().Be(typeof(GenericProxyTarget<ForwardTarget>));

            // A class deriving from an instantiation: the proxy of the instantiation, reporting the class.
            var derived = DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(GenericProxyTargetChild)).CreateInstance<ITypeNameProxy>(new GenericProxyTargetChild());
            derived.Name.Should().Be(nameof(String));
            ((IDuckType)derived).Type.Should().Be(typeof(GenericProxyTargetChild));

            // No activator for the type arguments (here, a value type the activator's constraint rejects): no proxy.
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(GenericProxyTarget<int>)).CanCreate().Should().BeFalse();
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(ForwardTarget)).CanCreate().Should().BeFalse();
        }

        [Fact]
        public void GenericProxyRegistrationServesDerivedClassesOnlyWhenDeclared()
        {
            DuckTypeAotEngine.RegisterGenericProxy(
                typeof(ITypeNameProxy),
                typeof(GenericProxyTarget<>),
                derivedTypes: false,
                arguments => Activator.CreateInstance(typeof(GenericProxyActivator<>).MakeGenericType(arguments))!);

            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(GenericProxyTarget<string>)).CanCreate().Should().BeTrue();
            DuckTypeAotEngine.GetOrCreateProxyType(typeof(ITypeNameProxy), typeof(GenericProxyTargetChild)).CanCreate().Should().BeFalse();
        }

        [Fact]
        public void GenericProxyRegistrationRequiresAGenericTypeDefinition()
        {
            Action register = () => DuckTypeAotEngine.RegisterGenericProxy(typeof(ITypeNameProxy), typeof(GenericProxyTarget<string>), derivedTypes: false, _ => new object());
            register.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(DerivedTypesSealed))]
        [InlineData(typeof(object[]))]
        public void DerivedTypesProxyRegistrationRequiresATypeWithDerivedClasses(Type targetType)
        {
            Action register = () => DuckTypeAotEngine.RegisterFallbackProxy(
                typeof(ITypeNameProxy),
                targetType,
                typeof(DisposableNameGeneratedProxy),
                (instance, type) => new DisposableNameGeneratedProxy(instance!, type),
                derivedTypes: true);
            register.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ReplayedFailuresKeepTheExceptionDynamicDuckTypingWraps()
        {
            var failure = DuckTypeAotRegisteredFailureException.Create(typeof(DuckTypeException).FullName!, "Error creating duck type", typeof(TypeLoadException).FullName!, "Method 'get_Name' does not have an implementation.");
            failure.Should().BeOfType<DuckTypeException>().Which.Message.Should().Be("Error creating duck type");
            failure.InnerException.Should().BeOfType<TypeLoadException>().Which.Message.Should().Be("Method 'get_Name' does not have an implementation.");
        }

        [Fact]
        public void ReverseProxyTypesShouldBeRecordedAsTheTypeTheyWereCreatedFor()
        {
            // The discovery records a forward duck cast over a reverse proxy with the type the reverse proxy was created for as
            // the target, for an interface contract as for a class one: the registry serves its generated reverse proxy types with
            // that mapping.
            DuckType.GetDynamicReverseProxyDefinitionType(DuckType.CreateReverse(typeof(IRecordedContract), new RecordedDelegation()).GetType()).Should().Be(typeof(IRecordedContract));
            DuckType.GetDynamicReverseProxyDefinitionType(DuckType.CreateReverse(typeof(RecordedContract), new RecordedDelegation()).GetType()).Should().Be(typeof(RecordedContract));
            DuckType.GetDynamicReverseProxyDefinitionType(DuckType.Create<IRecordedContract>(new RecordedDelegation())!.GetType()).Should().BeNull();
        }

        [Fact]
        public void ProxyTypeLookupShouldNotBeInlinedIntoCreateCallSites()
        {
            // The AOT mode dispatch stays out of the callers of DuckType.Create<T> and CreateCache<T>.GetProxy: their code size is
            // the one of dynamic duck typing.
            var lookup = typeof(DuckType).GetMethod("GetOrCreateProxyType", BindingFlags.NonPublic | BindingFlags.Static, binder: null, [typeof(Type), typeof(Type), typeof(bool)], modifiers: null);
            lookup.Should().NotBeNull();
            lookup!.MethodImplementationFlags.Should().HaveFlag(MethodImplAttributes.NoInlining);

            // Nor the creation of object-based proxies (Create(Type, object), DuckAs, TryDuckCast...).
            var createInstance = typeof(DuckType.CreateTypeResult).GetMethod("CreateInstance", BindingFlags.NonPublic | BindingFlags.Instance, binder: null, [typeof(object)], modifiers: null);
            createInstance.Should().NotBeNull();
            createInstance!.MethodImplementationFlags.Should().HaveFlag(MethodImplAttributes.NoInlining);
        }

        [Fact]
        public void TypedActivatorRegistrationsShouldServeTypedAndObjectCreation()
        {
            // The typed activator of a registry (CreateProxyInstance<TProxy>) is bound to its object activator: CreateInstance<T>
            // calls it directly, like dynamic duck typing's, and object-based creation calls the object activator.
            var objectActivatorCalls = 0;
            Func<object?, object?> objectActivator = instance =>
            {
                objectActivatorCalls++;
                return new ForwardGeneratedProxy((ForwardTarget)instance!);
            };
            var typedActivator = Delegate.CreateDelegate(
                typeof(CreateProxyInstance<IForwardProxy>),
                objectActivator,
                typeof(DuckTypeAotEngineTests).GetMethod(nameof(CreateTypedForwardProxy), BindingFlags.NonPublic | BindingFlags.Static)!);
            DuckTypeAotEngine.RegisterTypedProxy(typeof(IForwardProxy), typeof(ForwardTarget), typeof(ForwardGeneratedProxy), typedActivator);

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            result.CreateInstance<IForwardProxy>(new ForwardTarget("typed")).Value.Should().Be("typed");
            objectActivatorCalls.Should().Be(0);
            ((IForwardProxy)result.CreateInstance(new ForwardTarget("object"))).Value.Should().Be("object");
            objectActivatorCalls.Should().Be(1);

            // Like the cast of dynamic duck typing's typed activator to CreateProxyInstance<T> for another T.
            Action createOther = () => result.CreateInstance<object>(new ForwardTarget("other"));
            createOther.Should().Throw<InvalidCastException>()
                       .WithMessage($"Unable to cast object of type '{typeof(CreateProxyInstance<IForwardProxy>)}' to type '{typeof(CreateProxyInstance<object>)}'.");
        }

        [Fact]
        public void FastPathStoresShouldTakeTheResultByReference()
        {
            // CreateCache<T>.GetProxy and GetReverseProxy are inlined into their callers: storing the fast path entry doesn't copy
            // the CreateTypeResult at each of them.
            foreach (var name in new[] { "StoreForwardFastPath", "StoreReverseFastPath" })
            {
                var store = typeof(DuckType.CreateCache<IForwardProxy>).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
                store.Should().NotBeNull();
                var parameter = store!.GetParameters()[0];
                parameter.ParameterType.IsByRef.Should().BeTrue();
                parameter.IsIn.Should().BeTrue();
            }
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
            AssertFailureResultHasNoProxyActivator(result);
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<DuckTypeTargetMethodNotFoundException>()
                        .WithMessage(expectedMessage);
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<DuckTypeTargetMethodNotFoundException>()
                       .WithMessage(expectedMessage);
        }

        [Theory]
        [InlineData(typeof(DuckTypePropertyOrFieldNotFoundException))]
        [InlineData(typeof(DuckTypePropertyCantBeWrittenException))]
        [InlineData(typeof(DuckTypeFieldIsReadonlyException))]
        public void RegisterFailureUsingKnownExceptionTypeReplaysDescriptiveMessage(Type exceptionType)
        {
            DuckTypeAotEngine.RegisterProxyFailure(
                typeof(IMissingProxy),
                typeof(MissingTarget),
                exceptionType);

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IMissingProxy), typeof(MissingTarget));

            var failure = Record.Exception(() => _ = result.ProxyType);
            failure.Should().NotBeNull().And.BeOfType(exceptionType);
            failure!.Message.Should().Contain(typeof(IMissingProxy).FullName).And.Contain(typeof(MissingTarget).FullName);
        }

        [Fact]
        public void RegisterFailureFactoryReplaysNonDuckTypeException()
        {
            DuckTypeAotEngine.RegisterProxyFailureFactory(
                typeof(IForwardProxy),
                typeof(ForwardTarget),
                static () => new InvalidOperationException("factory failure"));

            var result = DuckTypeAotEngine.GetOrCreateProxyType(typeof(IForwardProxy), typeof(ForwardTarget));

            result.CanCreate().Should().BeFalse();
            Action getProxyType = () => _ = result.ProxyType;
            getProxyType.Should().Throw<InvalidOperationException>().WithMessage("factory failure");
            Action createProxy = () => _ = result.CreateInstance<IForwardProxy>(new ForwardTarget("failure"));
            createProxy.Should().Throw<InvalidOperationException>().WithMessage("factory failure");
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
        public void IsDynamicCodeSupportedIsTrueUnderTheJit()
        {
            // The module initializer of a generated registry doesn't initialize it then, on every runtime (some don't define
            // RuntimeFeature.IsDynamicCodeSupported).
            DuckTypeAotEngine.IsDynamicCodeSupported.Should().BeTrue();
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

        private interface IArrayLengthProxy
        {
            int Length { get; }
        }

        private class ArrayLengthGeneratedProxy : IArrayLengthProxy, IDuckType
        {
            private readonly Array _instance;
            private readonly Type _type;

            public ArrayLengthGeneratedProxy(Array instance, Type type)
            {
                _instance = instance;
                _type = type;
            }

            public int Length => _instance.Length;

            public object Instance => _instance;

            public Type Type => _type;

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>() => throw new NotSupportedException();

            public override string ToString() => _instance.ToString()!;
        }

        public interface IRecordedContract
        {
            string Name { get; }
        }

        public abstract class RecordedContract
        {
            public abstract string Name { get; }
        }

        public class RecordedDelegation
        {
            [DuckReverseMethod]
            public string Name => "name";
        }

        private interface ITypeNameProxy
        {
            string Name { get; }
        }

        private class TypeNameGeneratedProxy : ITypeNameProxy, IDuckType
        {
            private readonly Type _instance;
            private readonly Type _type;

            public TypeNameGeneratedProxy(Type instance, Type type)
            {
                _instance = instance;
                _type = type;
            }

            public string Name => _instance.Name;

            public object Instance => _instance;

            public Type Type => _type;

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>() => throw new NotSupportedException();

            public override string ToString() => _instance.ToString();
        }

        private class DisposableNameGeneratedProxy : ITypeNameProxy, IDuckType
        {
            private readonly object _instance;
            private readonly Type _type;

            public DisposableNameGeneratedProxy(object instance, Type type, string name = "disposable")
            {
                _instance = instance;
                _type = type;
                Name = name;
            }

            public string Name { get; }

            public object Instance => _instance;

            public Type Type => _type;

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>() => throw new NotSupportedException();

            public override string ToString() => _instance.ToString()!;
        }

        private interface IForwardProxy
        {
            string Value { get; }
        }

        private interface IDerivedTypesContract
        {
        }

        private class GenericProxyTarget<T>
        {
        }

        private class GenericProxyTargetChild : GenericProxyTarget<string>
        {
        }

        private sealed class GenericProxyActivator<T> : IDuckTypeAotGenericProxyActivator
            where T : class
        {
            public object? CreateInstance(object? instance, Type targetType) => new DisposableNameGeneratedProxy(instance!, targetType, typeof(T).Name);

            public Type GetProxyType() => typeof(DisposableNameGeneratedProxy);
        }

        private class DerivedTypesBase
        {
        }

        private class DerivedTypesChild : DerivedTypesBase
        {
        }

        private class DerivedTypesGrandChild : DerivedTypesChild, IDerivedTypesContract
        {
        }

        private class DerivedTypesContractImplementation : IDerivedTypesContract
        {
        }

        private sealed class DerivedTypesSealed
        {
        }

        private class ForwardTarget
        {
            public ForwardTarget(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private static IForwardProxy CreateTypedForwardProxy(Func<object?, object?> objectActivator, object? instance)
            => new ForwardGeneratedProxy((ForwardTarget)instance!);

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

            [DuckReverseMethod]
            public string Value { get; }
        }

        private class SecondSharedForwardReverseTarget : SharedForwardReverseTarget
        {
            public SecondSharedForwardReverseTarget(string value)
                : base(value)
            {
            }
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

        // The object activator of a generated proxy, without the wrapper that knows its proxy definition type.
        private static Delegate? GetGeneratedObjectActivator(DuckType.CreateTypeResult result)
        {
            var activator = GetCreateTypeResultField<Delegate>(result, "_activator");
            return activator?.Target is { } target && target.GetType().Name == "ObjectActivator"
                       ? (Delegate?)target.GetType().GetField("_activator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)
                       : activator;
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

        private static void AssertFailureResultHasNoProxyActivator(DuckType.CreateTypeResult result)
        {
            // The activator slot of a failure only holds the Action that throws it (see CreateTypeResult).
            result.UsesDynamicInvokeFallback.Should().BeFalse();
            GetCreateTypeResultField<Delegate>(result, "_activator").Should().BeAssignableTo<Action>();
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
