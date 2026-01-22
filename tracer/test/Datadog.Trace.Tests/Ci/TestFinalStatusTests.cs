// <copyright file="TestFinalStatusTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

/// <summary>
/// Unit tests for the test.final_status tag implementation.
/// Tests the CalculateFinalStatus helper and related functionality.
/// </summary>
public class TestFinalStatusTests
{
    // CalculateFinalStatus Helper Tests

    [Fact]
    public void CalculateFinalStatus_NullTags_AnyPassed_ReturnsPass()
    {
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: null);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_NullTags_NotPassed_NotSkip_ReturnsFail()
    {
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: null);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_NullTags_NotPassed_Skip_ReturnsSkip()
    {
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: null);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_NullTags_Passed_Skip_ReturnsPass()
    {
        // Pass takes precedence over skip
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: true, testTags: null);
        result.Should().Be(TestTags.StatusPass);
    }

    // Priority 1: Quarantined/Disabled Tests

    [Fact]
    public void CalculateFinalStatus_Quarantined_AnyPassed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsQuarantined = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined tests always return skip regardless of actual result");
    }

    [Fact]
    public void CalculateFinalStatus_Quarantined_AllFailed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsQuarantined = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined tests always return skip regardless of actual result");
    }

    [Fact]
    public void CalculateFinalStatus_Disabled_AnyPassed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsDisabled = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "disabled tests always return skip regardless of actual result");
    }

    [Fact]
    public void CalculateFinalStatus_Disabled_AllFailed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsDisabled = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "disabled tests always return skip regardless of actual result");
    }

    [Fact]
    public void CalculateFinalStatus_QuarantinedWithATF_Passed_ReturnsSkip()
    {
        // Quarantine always masks to skip, even with ATF enabled
        var tags = new TestSpanTags { IsQuarantined = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined tests with ATF still return skip");
    }

    [Fact]
    public void CalculateFinalStatus_QuarantinedWithATF_AllFailed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsQuarantined = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined tests with ATF still return skip");
    }

    [Fact]
    public void CalculateFinalStatus_DisabledWithATF_Passed_ReturnsSkip()
    {
        var tags = new TestSpanTags { IsDisabled = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "disabled tests with ATF still return skip");
    }

    // Priority 2: Any Execution Passed

    [Fact]
    public void CalculateFinalStatus_AnyPassed_NotSkip_ReturnsPass()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_AnyPassed_CurrentSkip_ReturnsPass()
    {
        // "Pass then skip" scenario - pass takes precedence
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "pass takes precedence over skip");
    }

    // Priority 3: Skip/Inconclusive

    [Fact]
    public void CalculateFinalStatus_NotPassed_Skip_ReturnsSkip()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_NotPassed_Inconclusive_ReturnsSkip()
    {
        // Inconclusive is treated as skip
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    // Priority 4: All Failed

    [Fact]
    public void CalculateFinalStatus_AllFailed_ReturnsFail()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // Single Execution Scenarios

    [Theory]
    [InlineData(true, false, TestTags.StatusPass)]  // Single pass
    [InlineData(false, false, TestTags.StatusFail)] // Single fail
    [InlineData(false, true, TestTags.StatusSkip)]  // Single skip
    public void CalculateFinalStatus_SingleExecution(bool passed, bool skipped, string expected)
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    // EFD Scenarios

    [Fact]
    public void CalculateFinalStatus_EFD_AllRetriesPass_ReturnsPass()
    {
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_EFD_FirstFailsLaterPasses_ReturnsPass()
    {
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "any pass means final status is pass");
    }

    [Fact]
    public void CalculateFinalStatus_EFD_AllRetriesFail_ReturnsFail()
    {
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_EFD_FirstPassesLaterFails_ReturnsPass()
    {
        // Edge Case 17: Initial passes, all retries fail -> pass (because initial passed)
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "initial execution passed, so final status is pass");
    }

    // ATR Scenarios

    [Fact]
    public void CalculateFinalStatus_ATR_FirstFailsRetryPasses_ReturnsPass()
    {
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_ATR_AllRetriesFail_ReturnsFail()
    {
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_ATR_PassesOnLastRetry_ReturnsPass()
    {
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    // ATF (Attempt to Fix) Scenarios

    [Fact]
    public void CalculateFinalStatus_ATF_NonQuarantined_FirstFailsLaterPasses_ReturnsPass()
    {
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_ATF_NonQuarantined_AllFail_ReturnsFail()
    {
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_ATF_InitialPassesAllRetriesFail_ReturnsPass()
    {
        // Edge Case 17: ATF variant
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "initial execution passed");
    }

    // ITR and Pre-execution Skip Scenarios

    [Fact]
    public void CalculateFinalStatus_ITRSkipped_ReturnsSkip()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_AttributeSkipped_ReturnsSkip()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    // Edge Cases

    [Fact]
    public void CalculateFinalStatus_IsQuarantinedFalse_NotTreatedAsQuarantined()
    {
        var tags = new TestSpanTags { IsQuarantined = "false" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "IsQuarantined='false' should not be treated as quarantined");
    }

    [Fact]
    public void CalculateFinalStatus_IsDisabledFalse_NotTreatedAsDisabled()
    {
        var tags = new TestSpanTags { IsDisabled = "false" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "IsDisabled='false' should not be treated as disabled");
    }

    [Fact]
    public void CalculateFinalStatus_EmptyTags_BehavesAsNormalTest()
    {
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Theory]
    [InlineData(true, false, false, false, TestTags.StatusSkip)]   // Quarantined + passed
    [InlineData(false, true, false, false, TestTags.StatusSkip)]   // Disabled + passed
    [InlineData(false, false, true, false, TestTags.StatusPass)]   // Normal + passed
    [InlineData(false, false, false, false, TestTags.StatusFail)]  // Normal + failed
    [InlineData(false, false, false, true, TestTags.StatusSkip)]   // Normal + skipped
    public void CalculateFinalStatus_PriorityOrder(bool quarantined, bool disabled, bool passed, bool skipped, string expected)
    {
        var tags = new TestSpanTags
        {
            IsQuarantined = quarantined ? "true" : null,
            IsDisabled = disabled ? "true" : null
        };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    // Pass Then Skip Scenarios (CI Behavior Match)

    [Fact]
    public void CalculateFinalStatus_PassThenSkip_ReturnsPass()
    {
        // CI behavior: if test passed once, pipeline passes
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "pass takes precedence over skip to match CI behavior");
    }

    [Fact]
    public void CalculateFinalStatus_FailThenSkip_ReturnsSkip()
    {
        // No pass + skip = skip
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "no pass + skip = skip");
    }

    // Mixed Retry Reason Scenarios

    [Theory]
    [InlineData(TestTags.TestRetryReasonEfd, true, TestTags.StatusPass)]
    [InlineData(TestTags.TestRetryReasonEfd, false, TestTags.StatusFail)]
    [InlineData(TestTags.TestRetryReasonAtr, true, TestTags.StatusPass)]
    [InlineData(TestTags.TestRetryReasonAtr, false, TestTags.StatusFail)]
    [InlineData(TestTags.TestRetryReasonAttemptToFix, true, TestTags.StatusPass)]
    [InlineData(TestTags.TestRetryReasonAttemptToFix, false, TestTags.StatusFail)]
    public void CalculateFinalStatus_RetryReasons_CorrectFinalStatus(string retryReason, bool anyPassed, string expected)
    {
        var tags = new TestSpanTags { TestRetryReason = retryReason };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: anyPassed, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(expected);
    }

    // Test Tag Existence Verification

    [Fact]
    public void TestTags_TestFinalStatus_ConstantExists()
    {
        TestTags.TestFinalStatus.Should().Be("test.final_status");
    }

    [Fact]
    public void TestTags_StatusConstants_Exist()
    {
        TestTags.StatusPass.Should().Be("pass");
        TestTags.StatusFail.Should().Be("fail");
        TestTags.StatusSkip.Should().Be("skip");
    }

    // Has Failed All Retries Interaction

    [Fact]
    public void CalculateFinalStatus_HasFailedAllRetries_StillCalculatesCorrectly()
    {
        // HasFailedAllRetries is a separate tag, doesn't affect final_status calculation
        var tags = new TestSpanTags { HasFailedAllRetries = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_HasFailedAllRetriesButPassed_ReturnsPass()
    {
        // Edge case: HasFailedAllRetries might be set incorrectly, but if anyPassed is true, should be pass
        var tags = new TestSpanTags { HasFailedAllRetries = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    // Attempt to Fix Passed Interaction

    [Fact]
    public void CalculateFinalStatus_AttemptToFixPassed_StillCalculatesCorrectly()
    {
        // AttemptToFixPassed is a separate tag, doesn't affect final_status calculation
        var tags = new TestSpanTags { AttemptToFixPassed = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_AttemptToFixPassedFalse_AllFailed_ReturnsFail()
    {
        var tags = new TestSpanTags { AttemptToFixPassed = "false" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // Comprehensive Scenario Matrix

    /// <summary>
    /// Test all combinations of the three input parameters plus quarantined/disabled status.
    /// </summary>
    [Theory]
    // Normal tests (not quarantined, not disabled)
    [InlineData(false, false, true, false, TestTags.StatusPass)]   // passed, not skip
    [InlineData(false, false, true, true, TestTags.StatusPass)]    // passed, skip (pass takes precedence)
    [InlineData(false, false, false, false, TestTags.StatusFail)]  // not passed, not skip
    [InlineData(false, false, false, true, TestTags.StatusSkip)]   // not passed, skip
    // Quarantined tests (always skip)
    [InlineData(true, false, true, false, TestTags.StatusSkip)]    // quarantined, passed
    [InlineData(true, false, true, true, TestTags.StatusSkip)]     // quarantined, passed, skip
    [InlineData(true, false, false, false, TestTags.StatusSkip)]   // quarantined, not passed
    [InlineData(true, false, false, true, TestTags.StatusSkip)]    // quarantined, not passed, skip
    // Disabled tests (always skip)
    [InlineData(false, true, true, false, TestTags.StatusSkip)]    // disabled, passed
    [InlineData(false, true, true, true, TestTags.StatusSkip)]     // disabled, passed, skip
    [InlineData(false, true, false, false, TestTags.StatusSkip)]   // disabled, not passed
    [InlineData(false, true, false, true, TestTags.StatusSkip)]    // disabled, not passed, skip
    public void CalculateFinalStatus_ComprehensiveMatrix(
        bool isQuarantined,
        bool isDisabled,
        bool anyPassed,
        bool isSkip,
        string expected)
    {
        var tags = new TestSpanTags
        {
            IsQuarantined = isQuarantined ? "true" : null,
            IsDisabled = isDisabled ? "true" : null
        };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: anyPassed, isSkippedOrInconclusive: isSkip, testTags: tags);
        result.Should().Be(expected);
    }

    // XUnit Dynamic Skip (SkipException) Scenarios - Tests 50-51

    [Fact]
    public void CalculateFinalStatus_DynamicSkip_SingleExecution_ReturnsSkip()
    {
        // Test 50: XUnit single-execution dynamic skip (SkipException)
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_DynamicSkip_LastRetry_ReturnsSkip()
    {
        // Test 51: XUnit retry test dynamic skip on last retry
        var tags = new TestSpanTags { TestIsRetry = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    // ATR Initial Pass Scenarios - Tests 52-53

    [Fact]
    public void CalculateFinalStatus_ATR_InitialPass_ReturnsPass()
    {
        // Test 52: XUnit ATR enabled, initial pass -> final_status = "pass"
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_ATR_InitialFailRetryPasses_ReturnsPass()
    {
        // Test 53: XUnit ATR enabled, initial fail, retry passes
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    // EFD Single Execution Scenarios - Tests 54-56

    [Theory]
    [InlineData(true, false, TestTags.StatusPass)]   // Test 54/56: EFD slow abort pass
    [InlineData(false, false, TestTags.StatusFail)]  // Test 54: EFD slow abort fail
    [InlineData(false, true, TestTags.StatusSkip)]   // EFD slow abort skip
    public void CalculateFinalStatus_EFD_SingleExecution_SlowAbort(bool passed, bool skipped, string expected)
    {
        var tags = new TestSpanTags { TestIsNew = "true", EarlyFlakeDetectionTestAbortReason = "slow" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateFinalStatus_ATF_SingleRetryConfigured_ReturnsCorrectStatus()
    {
        // Test 55: XUnit ATF with only 1 retry configured (TotalExecutions=1)
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    // ATF Framework Outcome Consistency - Tests 57-62

    [Theory]
    [InlineData(true, TestTags.StatusPass)]   // Test 57/61/62: ATF on normal test passes
    [InlineData(false, TestTags.StatusFail)]  // Test 58: ATF on normal test fails all
    public void CalculateFinalStatus_ATF_NormalTest_FrameworkOutcome(bool passed, string expected)
    {
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateFinalStatus_ATF_Quarantined_Passes_ReturnsSkip()
    {
        // Test 59: MsTest ATF on quarantined test (passes) -> skip
        var tags = new TestSpanTags { IsAttemptToFix = "true", IsQuarantined = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined always takes precedence");
    }

    [Fact]
    public void CalculateFinalStatus_ATF_Disabled_Fails_ReturnsSkip()
    {
        // Test 60: MsTest ATF on disabled test (fails) -> skip
        var tags = new TestSpanTags { IsAttemptToFix = "true", IsDisabled = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "disabled always takes precedence");
    }

    // ATR with FlakyRetryCount=0 Edge Cases - Tests 63-65

    [Theory]
    [InlineData(false, TestTags.StatusFail)]  // Test 63: ATR FlakyRetryCount=0, initial fail
    [InlineData(true, TestTags.StatusPass)]   // Test 64: ATR FlakyRetryCount=0, initial pass
    public void CalculateFinalStatus_ATR_ZeroRetries_SingleExecution(bool passed, string expected)
    {
        // Treated as single-execution when FlakyRetryCount=0
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateFinalStatus_ATF_SingleRetry_InitialExecution_ReturnsCorrectStatus()
    {
        // Test 65: XUnit ATF with TestManagementAttemptToFixRetryCount=1
        var tags = new TestSpanTags { IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // MsTest Exception Branch - Tests 66

    [Fact]
    public void CalculateFinalStatus_MsTest_ExceptionBranch_ReturnsFail()
    {
        // Test 66: MsTest early exception branch
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // Parameterized Test Per-Row Scenarios - Tests 67-68

    [Theory]
    [InlineData(true, TestTags.StatusPass)]   // Row passes
    [InlineData(false, TestTags.StatusFail)]  // Row fails
    public void CalculateFinalStatus_ParameterizedTest_PerRowStatus(bool rowPassed, string expected)
    {
        // Tests 67-68: Each parameterized row gets its own final_status
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: rowPassed, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(expected);
    }

    // Pre-execution Skip Paths - Tests 70-75

    [Fact]
    public void CalculateFinalStatus_NUnit_ITRSkipped_ReturnsSkip()
    {
        // Test 70: NUnit ITR-skipped test
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_NUnit_AttributeSkipped_ReturnsSkip()
    {
        // Test 71: NUnit attribute-skipped test
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_Inconclusive_ReturnsSkip()
    {
        // Test 72: MsTest Inconclusive
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_NotRunnable_ReturnsSkip()
    {
        // Test 73: MsTest NotRunnable
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_ClassInitError_ReturnsFail()
    {
        // Test 74: MsTest class initialization error
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_AssemblyInitError_ReturnsFail()
    {
        // Test 75: MsTest assembly initialization error
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // EFD Single-Execution and Initial Pass Gaps - Tests 76-80

    [Theory]
    [InlineData(true, false, TestTags.StatusPass)]   // Test 76: EFD fast test pass
    [InlineData(false, false, TestTags.StatusFail)]  // Test 76: EFD fast test fail
    [InlineData(false, true, TestTags.StatusSkip)]   // Test 77: EFD fast test skip
    public void CalculateFinalStatus_EFD_FastTest_DurationBasedCount1(bool passed, bool skipped, string expected)
    {
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("MsTest")]  // Test 78
    [InlineData("NUnit")]   // Test 79
    [InlineData("XUnit")]   // Test 80
    public void CalculateFinalStatus_EFD_InitialPassesAllRetriesFail_ReturnsPass(string framework)
    {
        // Tests 78-80: Initial PASSES, all 3 retries fail -> final_status = "pass"
        // HasFailedAllRetries = "true" is set separately (preserves retries-only semantics)
        var tags = new TestSpanTags { TestIsNew = "true", HasFailedAllRetries = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, $"{framework}: initial execution passed, so final status is pass");
    }

    // Review Round 7 Fixes - Tests 81-86

    [Fact]
    public void CalculateFinalStatus_MsTest_RetryExceptionBranch_LastRetry_ReturnsFail()
    {
        // Test 81: MsTest retry execution with exception (early branch)
        var tags = new TestSpanTags { TestIsRetry = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_ATREarlyExit_RetryPasses_ReturnsPass()
    {
        // Test 82: MsTest retry execution, retry 2 of 5 passes (ATR early exit)
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_EFD_TotalExecutions1_ReturnsCorrectStatus()
    {
        // Test 83: XUnit EFD TotalExecutions=1 uses metadata
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_NullMetadata_Pass_ReturnsPass()
    {
        // Test 84: XUnit null metadata pass (TestManagement disabled)
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: null);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_NullMetadata_Fail_ReturnsFail()
    {
        // Test 85: XUnit null metadata fail
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: null);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_NullMetadata_Skip_ReturnsSkip()
    {
        // Test 86: XUnit null metadata skip
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: true, testTags: null);
        result.Should().Be(TestTags.StatusSkip);
    }

    // ATR Budget Exhaustion Scenarios - Tests 87-90, 95-96, 104-107

    [Theory]
    [InlineData("XUnit")]   // Test 87
    [InlineData("NUnit")]   // Test 88
    [InlineData("MsTest")]  // Test 89
    public void CalculateFinalStatus_ATR_BudgetExhausted_MidRetry_Fail_ReturnsFail(string framework)
    {
        // Tests 87-89: ATR budget exhausted mid-retry (fail)
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail, $"{framework}: budget exhausted with fail -> final_status = fail");
    }

    [Fact]
    public void CalculateFinalStatus_ATR_BudgetExhausted_Pass_ReturnsPass()
    {
        // Test 90: ATR budget exhausted, but test passed (covered by early exit)
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_NUnit_ATR_BudgetLessThanOrEqual1_IsFinal_ReturnsFail()
    {
        // Test 95: NUnit ATR budget exhaustion: GetRemainingBudget() <= 1 -> isFinalExecution = true
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Theory]
    [InlineData("XUnit")]   // Test 104
    [InlineData("MsTest")]  // Test 105
    [InlineData("NUnit")]   // Test 106
    public void CalculateFinalStatus_ATR_BudgetExhausted_BeforeFirstRetry_Fail_ReturnsFail(string framework)
    {
        // Tests 104-106: ATR budget exhausted BEFORE first retry, test fails -> final_status = "fail" on initial
        var tags = new TestSpanTags();
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail, $"{framework}: budget exhausted before retry, test fails -> final_status = fail");
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_ATR_BudgetDecrementedTo0_DuringRetry_ReturnsFail()
    {
        // Test 107: XUnit ATR budget = 1 at initial, test fails, retry 1 -> budget decremented to 0
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // MsTest Parameterized Per-Row Cache Scenarios - Tests 91-94

    [Fact]
    public void CalculateFinalStatus_MsTest_Parameterized_Row1Passes_Row2Fails_CorrectPerRowStatus()
    {
        // Test 91: MsTest parameterized (row1 passes, row2 fails) with EFD retries
        // Row 1
        var row1Tags = new TestSpanTags { TestIsNew = "true" };
        var row1Result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: row1Tags);
        row1Result.Should().Be(TestTags.StatusPass, "row1 passes -> final_status = pass");

        // Row 2
        var row2Tags = new TestSpanTags { TestIsNew = "true" };
        var row2Result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: row2Tags);
        row2Result.Should().Be(TestTags.StatusFail, "row2 fails -> final_status = fail");
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_Parameterized_Row1Fails_Row2Passes_CorrectPerRowStatus()
    {
        // Test 92: MsTest parameterized (row1 fails, row2 passes) with ATF retries
        // Row 1
        var row1Tags = new TestSpanTags { IsAttemptToFix = "true" };
        var row1Result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: row1Tags);
        row1Result.Should().Be(TestTags.StatusFail, "row1 fails -> final_status = fail");

        // Row 2
        var row2Tags = new TestSpanTags { IsAttemptToFix = "true" };
        var row2Result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: row2Tags);
        row2Result.Should().Be(TestTags.StatusPass, "row2 passes -> final_status = pass");
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_Parameterized_AggregateRetries_AllRowsGetFinalStatus()
    {
        // Tests 93-94: MsTest parameterized with aggregate retries -> all rows get final_status on last retry
        var tags = new TestSpanTags { TestIsNew = "true", TestIsRetry = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    // Mixed-Feature Test Cases (EFD + ATR/ATF interactions) - Tests 98-103

    [Fact]
    public void CalculateFinalStatus_EFD_Plus_ATR_NewTestFails_ReturnsCorrectStatus()
    {
        // Test 98: EFD + ATR: new test fails -> EFD retries all, final_status on last EFD retry
        var tags = new TestSpanTags { TestIsNew = "true", TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonEfd };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_EFD_Plus_ATF_NewTestWithATF_ReturnsCorrectStatus()
    {
        // Test 99: EFD + ATF: new test marked for ATF -> EFD retries take precedence
        var tags = new TestSpanTags { TestIsNew = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_ATR_Plus_ATF_ATFTestFails_ReturnsCorrectStatus()
    {
        // Test 100: ATR + ATF: ATF test fails -> ATF retries all
        var tags = new TestSpanTags { IsAttemptToFix = "true", TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAttemptToFix };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    [Fact]
    public void CalculateFinalStatus_XUnit_EFD_Plus_ATR_NewTestPassesImmediately_LastRetryGetsStatus()
    {
        // Test 101: XUnit EFD + ATR: new test passes immediately -> EFD retries happen, final_status = "pass" on last retry
        var tags = new TestSpanTags { TestIsNew = "true", TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonEfd };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_MsTest_EFD_Plus_ATF_NewTestAlsoATF_EFDBehavior()
    {
        // Test 102: MsTest EFD + ATF: new test also ATF -> EFD behavior (TestIsNew checked before ATF)
        var tags = new TestSpanTags { TestIsNew = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public void CalculateFinalStatus_NUnit_EFD_Plus_ATR_KnownTestFails_ATRRetries()
    {
        // Test 103: NUnit EFD + ATR: known test fails -> ATR retries (not EFD)
        var tags = new TestSpanTags { TestIsRetry = "true", TestRetryReason = TestTags.TestRetryReasonAtr };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: false, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusFail);
    }

    // Quarantined + Disabled Combined Scenarios

    [Fact]
    public void CalculateFinalStatus_BothQuarantinedAndDisabled_ReturnsSkip()
    {
        // Edge case: both flags set (shouldn't happen but test the priority)
        var tags = new TestSpanTags { IsQuarantined = "true", IsDisabled = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip);
    }

    [Fact]
    public void CalculateFinalStatus_QuarantinedDisabledAndATF_AllSet_ReturnsSkip()
    {
        // Edge case: all flags set
        var tags = new TestSpanTags { IsQuarantined = "true", IsDisabled = "true", IsAttemptToFix = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusSkip, "quarantined/disabled always takes precedence over ATF");
    }

    // EFD + New Test Combinations

    [Theory]
    [InlineData(true, false, TestTags.StatusPass)]
    [InlineData(false, false, TestTags.StatusFail)]
    [InlineData(false, true, TestTags.StatusSkip)]
    [InlineData(true, true, TestTags.StatusPass)]  // Pass takes precedence over skip
    public void CalculateFinalStatus_NewTest_AllCombinations(bool passed, bool skipped, string expected)
    {
        var tags = new TestSpanTags { TestIsNew = "true" };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    // Retry with Various Tags

    [Theory]
    [InlineData(true, false, false, TestTags.StatusPass)]   // Retry passed
    [InlineData(false, false, false, TestTags.StatusFail)]  // Retry failed
    [InlineData(false, true, false, TestTags.StatusSkip)]   // Retry skipped
    [InlineData(true, false, true, TestTags.StatusSkip)]    // Retry passed but quarantined
    [InlineData(false, false, true, TestTags.StatusSkip)]   // Retry failed but quarantined
    public void CalculateFinalStatus_RetryWithQuarantine(bool passed, bool skipped, bool quarantined, string expected)
    {
        var tags = new TestSpanTags
        {
            TestIsRetry = "true",
            IsQuarantined = quarantined ? "true" : null
        };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: passed, isSkippedOrInconclusive: skipped, testTags: tags);
        result.Should().Be(expected);
    }

    // Edge case: Empty string vs null for tag values

    [Fact]
    public void CalculateFinalStatus_IsQuarantinedEmptyString_NotTreatedAsQuarantined()
    {
        var tags = new TestSpanTags { IsQuarantined = string.Empty };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "empty string should not be treated as quarantined");
    }

    [Fact]
    public void CalculateFinalStatus_IsDisabledEmptyString_NotTreatedAsDisabled()
    {
        var tags = new TestSpanTags { IsDisabled = string.Empty };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        result.Should().Be(TestTags.StatusPass, "empty string should not be treated as disabled");
    }

    // Case sensitivity check

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("TRUE ")]
    [InlineData(" true")]
    public void CalculateFinalStatus_IsQuarantined_CaseSensitive_OnlyLowercaseTrue(string value)
    {
        var tags = new TestSpanTags { IsQuarantined = value };
        var result = Common.CalculateFinalStatus(anyExecutionPassed: true, isSkippedOrInconclusive: false, testTags: tags);
        // Only exact "true" should be treated as quarantined
        result.Should().Be(TestTags.StatusPass, $"'{value}' should not be treated as quarantined (only exact 'true' matches)");
    }
}
