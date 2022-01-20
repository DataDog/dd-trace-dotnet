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

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.AwsLambda), MemberType = typeof(PackageVersions))]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine"))
                && !string.IsNullOrEmpty(packageVersion))
            {
                Output.WriteLine("Skipping");
                return;
            }

            using (var agent = EnvironmentHelper.GetMockAgent(fixedPort: 5002))
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(4, 5000).Where(s => s.TraceId == 1111).ToArray();

                spans.OrderBy(s => s.Start);
                spans.Length.Should().Be(2);
                spans[0].Name.Should().Be("http.request");
                spans[0].Resource.Should().Be("GET datadoghq.com/");
                spans[0].Error.ToString().Should().Be("0");
                spans[0].ParentId.ToString().Should().Be("2222");

                spans[1].Name.Should().Be("placeholder-operation");
                spans[1].Error.ToString().Should().Be("0");
                spans[1].SpanId.ToString().Should().Be("2222");
            }
        }
    }
}
#endif
