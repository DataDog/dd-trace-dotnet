// <copyright file="AspNetCoreNetFramework22Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    public class AspNetCoreNetFramework22Tests : AspNetCoreNetFrameworkTestBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreNetFramework22Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreNetFramework", fixture, output)
        {
        }
    }
}

#endif
