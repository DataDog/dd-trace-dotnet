// <copyright file="AwsLambdaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System;
using System.Collections.Generic;
using System.Linq;

using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    public class AwsLambdaTests : TestHelper
    {
        public AwsLambdaTests(ITestOutputHelper output)
            : base("AWS.Lambda", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "ArmUnsupported")]
        [Trait("Category", "Lambda")]
        public void SubmitsTraces()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
            {
                Output.WriteLine("Skipping");
                return;
            }

            using (var agent = EnvironmentHelper.GetMockAgent(fixedPort: 5002))
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(9, 15000).Where(s => s.TraceId == 1111).ToArray();
                spans.OrderBy(s => s.Start);
                spans.Length.Should().Be(9);
                for (var i = 0; i < spans.Length; ++i)
                {
                    spans[i].ParentId.ToString().Should().Be("2222");
                    spans[i].TraceId.ToString().Should().Be("1111");
                    spans[i].Name.Should().Be("http.request");
                }

                spans[0].Resource.Should().Be("GET localhost/function/HandlerNoParamSync");
                spans[1].Resource.Should().Be("GET localhost/function/HandlerOneParamSync");
                spans[2].Resource.Should().Be("GET localhost/function/HandlerTwoParamsSync");
                spans[3].Resource.Should().Be("GET localhost/function/HandlerNoParamAsync");
                spans[4].Resource.Should().Be("GET localhost/function/HandlerOneParamAsync");
                spans[5].Resource.Should().Be("GET localhost/function/HandlerTwoParamsAsync");
                spans[6].Resource.Should().Be("GET localhost/function/HandlerNoParamVoid");
                spans[7].Resource.Should().Be("GET localhost/function/HandlerOneParamVoid");
                spans[8].Resource.Should().Be("GET localhost/function/HandlerTwoParamsVoid");
            }
        }
    }
}
#endif
