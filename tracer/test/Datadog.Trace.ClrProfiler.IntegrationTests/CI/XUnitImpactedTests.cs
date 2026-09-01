// <copyright file="XUnitImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
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
        private const string IsModifiedTag = "test.is_modified";

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
            return SubmitTests(packageVersion, 2, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task DisabledByEnvVar(string packageVersion)
        {
            InjectGitHubActionsSession(true, false);
            return SubmitTests(packageVersion, 0, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task EnabledBySettings(string packageVersion)
        {
            Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine due to a Detached Head status. An issue has been opened to handle the situation. Meanwhile we are skipping it.");

            InjectGitHubActionsSession(true, null);
            return SubmitTests(packageVersion, 2, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task GitBranchBasedImpactDetection(string packageVersion)
        {
            await SubmitTestsUsingGitBranch(packageVersion, 2, TestIsModified);
        }

        private static bool TestIsModified(MockCIVisibilityTest t) => t.Meta.ContainsKey(IsModifiedTag) && t.Meta[IsModifiedTag] == "true";
    }
}
