// <copyright file="AspNetCoreNetFramework21Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class AspNetCoreNetFramework21Tests : AspNetCoreNetFrameworkTestBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreNetFramework21Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreNetFramework21", "AspNetCoreNetFramework21", fixture, output)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class AspNetCoreNetFramework21DisabledTests : AspNetCoreNetFrameworkDisabledTestBase
    {
        public AspNetCoreNetFramework21DisabledTests(ITestOutputHelper output)
            : base("AspNetCoreNetFramework21", "AspNetCoreNetFramework21", output)
        {
        }
    }

    public class AspNetCoreNetFramework21ColdStartTests : AspNetCoreNetFrameworkColdStartTestBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCoreNetFramework21ColdStartTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreNetFramework21", fixture, output)
        {
        }
    }
}

#endif
