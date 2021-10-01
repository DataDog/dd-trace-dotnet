// <copyright file="StackExchangeRedisStackOverflowExceptionSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using System;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [NonParallelizable]
    public class StackExchangeRedisStackOverflowExceptionSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisStackOverflowExceptionSmokeTest()
            : base("StackExchange.Redis.StackOverflowException", maxTestRunSeconds: 30)
        {
        }

        [Test]
        [Property("Category", "Smoke")]
        public void NoExceptions()
        {
            if (EnvironmentTools.IsWindows())
            {
                Console.WriteLine("Ignored for Windows");
                return;
            }

            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
