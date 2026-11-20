// <copyright file="RemoteConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationManagerTests
{
    private static readonly RcmClientTracer Tracer = RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", null, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), null);
    private static readonly RemoteConfigEndpoint Endpoint = new("http://localhost:8126/", TimeSpan.FromSeconds(1));

    [Fact]
    public void CreateClientFactory_NativeClientEnabledAndAvailable_UsesTheNativeClient()
    {
        var log = new Mock<IDatadogLogger>();
        var native = new Mock<IRemoteConfigClient>().Object;
        var managedCalls = 0;

        var factory = RemoteConfigurationManager.CreateClientFactory(
            nativeClientEnabled: true,
            isLibDatadogAvailable: true,
            (_, _, _) => native,
            (_, _, _) =>
            {
                managedCalls++;
                return new Mock<IRemoteConfigClient>().Object;
            },
            log.Object);

        factory("client-id", Tracer, Endpoint).Should().BeSameAs(native);
        managedCalls.Should().Be(0);
        log.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(typeof(DllNotFoundException))]
    [InlineData(typeof(EntryPointNotFoundException))]
    [InlineData(typeof(LibDatadogException))]
    public void CreateClientFactory_NativeClientCreationFails_UsesTheManagedClientAndLogsAnError(Type exceptionType)
    {
        var log = new Mock<IDatadogLogger>();
        var managed = new Mock<IRemoteConfigClient>().Object;
        var error = (Exception)Activator.CreateInstance(exceptionType, "libdatadog failure");
        (string ClientId, RcmClientTracer Target, RemoteConfigEndpoint Endpoint) managedArguments = default;

        var factory = RemoteConfigurationManager.CreateClientFactory(
            nativeClientEnabled: true,
            isLibDatadogAvailable: true,
            (_, _, _) => throw error,
            (clientId, target, endpoint) =>
            {
                managedArguments = (clientId, target, endpoint);
                return managed;
            },
            log.Object);

        factory("client-id", Tracer, Endpoint).Should().BeSameAs(managed);
        managedArguments.Should().Be(("client-id", Tracer, Endpoint));
        log.Verify(l => l.Error(error, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CreateClientFactory_BothClientCreationsFail_Throws()
    {
        var factory = RemoteConfigurationManager.CreateClientFactory(
            nativeClientEnabled: true,
            isLibDatadogAvailable: true,
            (_, _, _) => throw new DllNotFoundException(),
            (_, _, _) => throw new InvalidOperationException("managed"),
            new Mock<IDatadogLogger>().Object);

        // RcmSubscriptionManager.Poll logs it, and polling stops
        factory.Invoking(f => f("client-id", Tracer, Endpoint)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false, true, "DD_REMOTE_CONFIGURATION_NATIVE_CLIENT_ENABLED is false")]
    [InlineData(true, false, "libdatadog is not available")]
    [InlineData(false, false, "DD_REMOTE_CONFIGURATION_NATIVE_CLIENT_ENABLED is false")]
    public void CreateClientFactory_NativeClientDisabledOrUnavailable_UsesTheManagedClient(bool nativeClientEnabled, bool isLibDatadogAvailable, string reason)
    {
        var log = new Mock<IDatadogLogger>();
        var managed = new Mock<IRemoteConfigClient>().Object;
        var nativeCalls = 0;

        var factory = RemoteConfigurationManager.CreateClientFactory(
            nativeClientEnabled,
            isLibDatadogAvailable,
            (_, _, _) =>
            {
                nativeCalls++;
                return new Mock<IRemoteConfigClient>().Object;
            },
            (_, _, _) => managed,
            log.Object);

        factory("client-id", Tracer, Endpoint).Should().BeSameAs(managed);
        nativeCalls.Should().Be(0);
        log.Verify(l => l.Information(It.IsAny<string>(), reason, It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }
}
