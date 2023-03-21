// <copyright file="AspNetCore5AsmAttributesWafTimeout.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single type       | contains alls test types
#pragma warning disable SA1649 // File name should match first type name    | contains alls test types

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class TestWafTimeoutValueChanged : AspNetCore5AsmAttributesWafTimeout
    {
        public TestWafTimeoutValueChanged(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, nameof(TestWafTimeoutValueChanged), "global", 1)
        {
        }

        [SkippableTheory]
        [InlineData("/params-endpoint/appscan_fingerprint", 200)]
        [Trait("RunOnWindows", "True")]
        public async Task RunTask(string type, int statusCode)
        {
            await RunTest(type, statusCode);
        }
    }

    public class TestWafLargeValueChanged : AspNetCore5AsmAttributesWafTimeout
    {
        public TestWafLargeValueChanged(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, nameof(TestWafLargeValueChanged), "global", 2000000000)
        {
        }

        [SkippableTheory]
        [InlineData("/params-endpoint/appscan_fingerprint", 200)]
        [Trait("RunOnWindows", "True")]
        public async Task RunTask(string type, int statusCode)
        {
            await RunTest(type, statusCode);
        }
    }

    public class TestWafInvalidNegativeValueChanged : AspNetCore5AsmAttributesWafTimeout
    {
        public TestWafInvalidNegativeValueChanged(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, nameof(TestWafInvalidNegativeValueChanged), "global", -1801)
        {
        }

        [SkippableTheory]
        [InlineData("/params-endpoint/appscan_fingerprint", 200)]
        [Trait("RunOnWindows", "True")]
        public async Task RunTask(string type, int statusCode)
        {
            await RunTest(type, statusCode);
        }
    }

    public class TestWafInvalidZeroValueChanged : AspNetCore5AsmAttributesWafTimeout
    {
        public TestWafInvalidZeroValueChanged(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, nameof(TestWafInvalidZeroValueChanged), "global", 0)
        {
        }

        [SkippableTheory]
        [InlineData("/params-endpoint/appscan_fingerprint", 200)]
        [Trait("RunOnWindows", "True")]
        public async Task RunTask(string type, int statusCode)
        {
            await RunTest(type, statusCode);
        }
    }

    public class AspNetCore5AsmAttributesWafTimeout : RcmBase
    {
        private const string AsmProduct = "ASM";

        private readonly string _id;
        private readonly int _timeout;
        private readonly string _testName;

        public AspNetCore5AsmAttributesWafTimeout(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string testName, string id, int timeout)
            : base(fixture, outputHelper, enableSecurity: true, testName: testName)
        {
            _id = id;
            _timeout = timeout;
            _testName = testName;

            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        }

        public async Task RunTest(string type, int statusCode)
        {
            EnableDebugMode();

            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings(type, statusCode);

            var spans1 = await SendRequestsAsync(agent, type);
            var acknowledgedId = _testName + Guid.NewGuid();

            var rcmWafData = CreateWafTimeoutRcm(_timeout, _id);
            await agent.SetupRcmAndWait(Output, new[] { ((object)rcmWafData, acknowledgedId) }, AsmProduct, appliedServiceNames: new[] { acknowledgedId });

            var spans2 = await SendRequestsAsync(agent, type);
            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            await VerifySpans(spans.ToImmutableList(), settings);
        }

        private Payload CreateWafTimeoutRcm(int timeoutValue, string id)
        {
            return new Payload { Data = new Data { Id = id, Type = "custom_attributes", Attributes = new Attributes { Values = new Dictionary<string, object> { { "waf_timeout", timeoutValue }, } } } };
        }
    }
}
#endif
