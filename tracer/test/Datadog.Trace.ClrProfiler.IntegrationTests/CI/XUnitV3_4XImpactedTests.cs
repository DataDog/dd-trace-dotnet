// <copyright file="XUnitV3_4XImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER && !DEFAULT_SAMPLES

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(ImpactedTestsCollection))]
public class XUnitV3_4XImpactedTests : TestingFrameworkImpactedTests
{
    private const string IsModifiedTag = "test.is_modified";

    public XUnitV3_4XImpactedTests(ITestOutputHelper output)
        : base(
            "XUnitTestsV3",
            "tracer/test/test-applications/integrations/Samples.XUnitTestsV3/TestSuite.cs",
            expectedTestCount: 16,
            useDotnetExec: true,
            [
                "_output.WriteLine(\"Test:SimplePassTest\");",
                "public void TraitSkipFromAttributeTest()",
            ],
            "-namespace Samples.XUnitTestsV3",
            output)
    {
        SetServiceName("xunit-v3-4x-impacted-tests");
        SetServiceVersion("1.0.0");
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task BaseShaFromPr([PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion)
    {
        InjectGitHubActionsSession();
        return SubmitTests(packageVersion, 2, TestIsModified);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task DisabledByEnvVar([PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion)
    {
        InjectGitHubActionsSession(true, false);
        return SubmitTests(packageVersion, 0, TestIsModified);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task EnabledBySettings([PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion)
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine due to detached HEAD handling.");
        InjectGitHubActionsSession(true, null);
        return SubmitTests(packageVersion, 2, TestIsModified);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task GitBranchBasedImpactDetection([PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion)
        => SubmitTestsUsingGitBranch(packageVersion, 2, TestIsModified);

    private static bool TestIsModified(MockCIVisibilityTest test)
        => test.Meta.ContainsKey(IsModifiedTag) && test.Meta[IsModifiedTag] == "true";
}
#endif
