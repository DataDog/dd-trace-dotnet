// <copyright file="XUnitV4ImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public class XUnitV4ImpactedTests : TestingFrameworkImpactedTests
{
    private const string IsModifiedTag = "test.is_modified";

    public XUnitV4ImpactedTests(ITestOutputHelper output)
        : base(
            "XUnitTestsV3",
            "tracer/test/test-applications/integrations/Samples.XUnitTestsV3/TestSuite.cs",
            expectedTestCount: 16,
            useDotnetExec: true,
            [
                "_output.WriteLine(\"Test:SimplePassTest\");",
                "public void TraitSkipFromAttributeTest()",
            ],
            output)
    {
        SetServiceName("xunit-v4-impacted-tests");
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task BaseShaFromPr()
    {
        InjectGitHubActionsSession();
        return SubmitTests("4.0.0", 2, TestIsModified);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task DisabledByEnvVar()
    {
        InjectGitHubActionsSession(true, false);
        return SubmitTests("4.0.0", 0, TestIsModified);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task EnabledBySettings()
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine due to detached HEAD handling.");
        InjectGitHubActionsSession(true, null);
        return SubmitTests("4.0.0", 2, TestIsModified);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public Task GitBranchBasedImpactDetection()
        => SubmitTestsUsingGitBranch("4.0.0", 2, TestIsModified);

    private static bool TestIsModified(MockCIVisibilityTest test)
        => test.Meta.ContainsKey(IsModifiedTag) && test.Meta[IsModifiedTag] == "true";
}
#endif
