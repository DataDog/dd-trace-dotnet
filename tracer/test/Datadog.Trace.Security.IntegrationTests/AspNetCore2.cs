// <copyright file="AspNetCore2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
using System;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore2 : AspNetBase
    {
        public AspNetCore2()
            : base("AspNetCore2", "/shutdown")
        {
        }

        // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
        [TestCase(true, true, HttpStatusCode.OK)]
        [TestCase(true, false, HttpStatusCode.OK)]
        [TestCase(false, true, HttpStatusCode.OK)]
        [TestCase(false, false, HttpStatusCode.OK)]
        [Property("RunOnWindows", "True")]
        [Property("Category", "ArmUnsupported")]
        public async Task TestSecurity(bool enableSecurity, bool enableBlocking, HttpStatusCode expectedStatusCode)
        {
            using var agent = await RunOnSelfHosted(enableSecurity, enableBlocking);
            await TestBlockedRequestAsync(agent, enableSecurity, expectedStatusCode, 5, new Action<TestHelpers.MockTracerAgent.Span>[]
            {
             s => Assert.AreEqual("aspnet_core.request", s.Name),
             s => Assert.AreEqual("Samples.AspNetCore2", s.Service),
             s => Assert.AreEqual("web", s.Type)
            });
        }
    }
}
#endif
