// <copyright file="MultipleAppsInDomainWithMixedPartialTrust.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS;

public class MultipleAppsInDomainWithMixedPartialTrust(ITestOutputHelper output) : MultipleAppsInDomainBase(output)
{
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("IIS", "True")]
    [Trait("MSI", "True")]
    public async Task ApplicationDoesNotReturnErrors()
        => await RunTest(app1Port: 8083, app2Port: 8084, expectedOutput: "DummyKey1 value from web.config", logDirectoryName: "MultipleAppsInDomain");
}
#endif
