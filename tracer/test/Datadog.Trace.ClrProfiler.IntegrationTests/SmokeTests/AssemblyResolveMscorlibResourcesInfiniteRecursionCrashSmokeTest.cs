// <copyright file="AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest : SmokeTestBase
    {
        public AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest()
            : base("AssemblyResolveMscorlibResources.InfiniteRecursionCrash")
        {
        }

        [Test]
        [Property("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
