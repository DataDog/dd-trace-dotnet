// <copyright file="AssemblyLoadContextRedirectTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class AssemblyLoadContextRedirectTest : SmokeTestBase
    {
        public AssemblyLoadContextRedirectTest(ITestOutputHelper output)
            : base(output, "AssemblyLoadContextRedirect")
        {
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
