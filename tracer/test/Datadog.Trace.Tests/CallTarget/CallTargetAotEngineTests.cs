// <copyright file="CallTargetAotEngineTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

#pragma warning disable CS0618 // Manual AOT registration APIs are intentionally exercised by compatibility tests.
#pragma warning disable SA1201 // Keep small test helpers close to the tests that use them.

namespace Datadog.Trace.Tests.CallTarget;

public class CallTargetAotEngineTests : IDisposable
{
    public CallTargetAotEngineTests()
    {
        CallTargetAot.ResetForTests();
    }

    public void Dispose()
    {
        CallTargetAot.ResetForTests();
    }

    private static string CurrentDatadogTraceAssemblyVersion => typeof(CallTargetAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private static string CurrentDatadogTraceAssemblyMvid => typeof(CallTargetAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

    [Fact]
    public void ValidateContractWithSchemaMismatchThrows()
    {
        Action validate = () => CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract("999", CurrentDatadogTraceAssemblyVersion, CurrentDatadogTraceAssemblyMvid),
            CreateCurrentRegistryMetadata());

        validate.Should().Throw<CallTargetAotRegistryContractValidationException>();
    }

    [Fact]
    public void ValidateContractWithDatadogTraceMismatchThrows()
    {
        Action validate = () => CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract(CallTargetAotContract.CurrentSchemaVersion, "0.0.0.0", CurrentDatadogTraceAssemblyMvid),
            CreateCurrentRegistryMetadata());

        validate.Should().Throw<CallTargetAotRegistryContractValidationException>();
    }

    [Fact]
    public void ValidateContractThenInitializeUsesValidatedRegistryIdentity()
    {
        CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract(
                CallTargetAotContract.CurrentSchemaVersion,
                CurrentDatadogTraceAssemblyVersion,
                CurrentDatadogTraceAssemblyMvid),
            new CallTargetAotAssemblyMetadata(
                "Fake.CallTarget.Registry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                Guid.NewGuid().ToString("D")));

        Action initialize = () => CallTargetAot.TryInitializeGeneratedRegistry(typeof(CallTargetAotEngineTests));

        initialize.Should().NotThrow("contract validation establishes the generated registry identity before NativeAOT remaps runtime ownership.");
    }

    [Fact]
    public void ValidateContractWithDifferentValidatedRegistryIdentityThrows()
    {
        CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract(
                CallTargetAotContract.CurrentSchemaVersion,
                CurrentDatadogTraceAssemblyVersion,
                CurrentDatadogTraceAssemblyMvid),
            new CallTargetAotAssemblyMetadata(
                "Fake.CallTarget.Registry.One, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                Guid.NewGuid().ToString("D")));

        Action validate = () => CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract(
                CallTargetAotContract.CurrentSchemaVersion,
                CurrentDatadogTraceAssemblyVersion,
                CurrentDatadogTraceAssemblyMvid),
            new CallTargetAotAssemblyMetadata(
                "Fake.CallTarget.Registry.Two, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                Guid.NewGuid().ToString("D")));

        validate.Should().Throw<CallTargetAotMultipleRegistryAssembliesException>();
    }

    [Fact]
    public void ConflictingHandlerRegistrationThrowsSpecificException()
    {
        CallTargetAot.EnableAotMode();

        CallTargetAot.RegisterAotHandlerPair(
            typeof(TestIntegration),
            typeof(TestTarget),
            returnType: null,
            declaringType: typeof(CallTargetAotEngineTests),
            beginMethodName: nameof(BeginHandlerA),
            endMethodName: nameof(EndHandlerA));

        Action conflictingRegistration = () => CallTargetAot.RegisterAotHandlerPair(
            typeof(TestIntegration),
            typeof(TestTarget),
            returnType: null,
            declaringType: typeof(CallTargetAotEngineTests),
            beginMethodName: nameof(BeginHandlerB),
            endMethodName: nameof(EndHandlerB));

        conflictingRegistration.Should().Throw<CallTargetAotRegistrationConflictException>();
    }

    private static CallTargetAotAssemblyMetadata CreateCurrentRegistryMetadata()
    {
        var assembly = typeof(CallTargetAotEngineTests).Assembly;
        return new CallTargetAotAssemblyMetadata(
            assembly.FullName ?? assembly.GetName().Name ?? "unknown",
            assembly.ManifestModule.ModuleVersionId.ToString("D"));
    }

    private static Datadog.Trace.ClrProfiler.CallTarget.CallTargetState BeginHandlerA(TestTarget instance) => default;

    private static Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn EndHandlerA(TestTarget instance, Exception exception, in Datadog.Trace.ClrProfiler.CallTarget.CallTargetState state) => default;

    private static Datadog.Trace.ClrProfiler.CallTarget.CallTargetState BeginHandlerB(TestTarget instance) => default;

    private static Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn EndHandlerB(TestTarget instance, Exception exception, in Datadog.Trace.ClrProfiler.CallTarget.CallTargetState state) => default;

    private sealed class TestIntegration;

    private sealed class TestTarget;
}

#pragma warning restore SA1201
#pragma warning restore CS0618
