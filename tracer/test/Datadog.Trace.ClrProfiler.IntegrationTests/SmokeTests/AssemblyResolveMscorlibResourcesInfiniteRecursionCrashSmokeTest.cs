// <copyright file="AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest : SmokeTestBase
    {
        public AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest(ITestOutputHelper output)
            : base(output, "AssemblyResolveMscorlibResources.InfiniteRecursionCrash")
        {
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task NoExceptions()
        {
            await CheckForSmoke();
        }
    }
}
