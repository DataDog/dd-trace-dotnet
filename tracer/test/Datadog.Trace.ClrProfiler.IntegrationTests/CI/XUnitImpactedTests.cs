// <copyright file="XUnitImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class XUnitImpactedTests : TestingFrameworkImpactedTests
    {
        private const int ExpectedSpanCount = 41;

        public XUnitImpactedTests(ITestOutputHelper output)
            : base("XUnitTests", output)
        {
            SetServiceName("xunit-tests");
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromPr(string packageVersion)
        {
            InjectGitHubActionsSession();

            SetEnvironmentVariable("DD_TRACE_DEBUG", "1");

            return SubmitTests(packageVersion, $"baseShaFromPr", 2, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
        }

/*
        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            return SubmitTests(packageVersion, $"baseShaFromPr", 2, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task FilesFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = (request, receivedTests) =>
            {
                if (request.PathAndQuery.EndsWith("ci/tests/diffs"))
                {
                    request.Response = new MockTracerResponse(GetDiffFilesJson(false), 200);
                    return;
                }

                ProcessAgentRequest(request, receivedTests);
            };
            return SubmitTests(packageVersion, $"baseShaFromPr", 12, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true", agentRequestProcessor);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task Disabled(string packageVersion)
        {
            InjectGitHubActionsSession(true, false);
            return SubmitTests(packageVersion, $"baseShaFromPr", 0, (t) => t.Meta.ContainsKey("is_modified"));
        }
*/
    }
}
