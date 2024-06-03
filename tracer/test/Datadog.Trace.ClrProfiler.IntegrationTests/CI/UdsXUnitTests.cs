// <copyright file="UdsXUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class UdsXUnitTests(ITestOutputHelper output) : XUnitTests(output)
{
    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public override Task SubmitTraces(string packageVersion)
    {
        EnvironmentHelper.EnableUnixDomainSockets();
        return base.SubmitTraces(packageVersion);
    }
}

#endif
