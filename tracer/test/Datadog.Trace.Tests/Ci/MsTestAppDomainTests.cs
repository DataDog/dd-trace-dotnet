// <copyright file="MsTestAppDomainTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class MsTestAppDomainTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscoveryCountSurvivesIsolationWithoutBeingCountedTwice(bool initializeExecutionBeforeDiscovery)
    {
        var discoveryDomain = CreateDomain();
        var executionDomain = CreateDomain();
        try
        {
            var discovery = CreateCounter(discoveryDomain);
            var execution = CreateCounter(executionDomain);
            if (initializeExecutionBeforeDiscovery)
            {
                discovery.CopyTo(executionDomain);
                execution.GetTotal().Should().Be(0);
            }

            discovery.Discover(10);
            discovery.CopyTo(executionDomain);

            execution.GetTotal().Should().Be(10);
            execution.GetTotal().Should().Be(10);
            discovery.GetTotal().Should().Be(10);
        }
        finally
        {
            AppDomain.Unload(executionDomain);
            AppDomain.Unload(discoveryDomain);
        }
    }

    private static AppDomain CreateDomain()
        => AppDomain.CreateDomain("MSTest discovery regression " + Guid.NewGuid(), null, new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.BaseDirectory });

    private static RemoteDiscoveryCounter CreateCounter(AppDomain domain)
        => (RemoteDiscoveryCounter)domain.CreateInstanceAndUnwrap(typeof(RemoteDiscoveryCounter).Assembly.FullName, typeof(RemoteDiscoveryCounter).FullName);

    public sealed class RemoteDiscoveryCounter : MarshalByRefObject
    {
        public void Discover(int count) => MsTestIntegration.AddTotalTestCases(count);

        public long GetTotal() => MsTestIntegration.GetTotalTestCases();

        public void CopyTo(AppDomain executionDomain) => MsTestIntegration.CopyTotalTestCasesTo(executionDomain);
    }
}
#endif
