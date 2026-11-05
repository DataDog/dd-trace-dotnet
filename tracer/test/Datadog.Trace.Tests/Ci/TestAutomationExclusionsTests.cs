// <copyright file="TestAutomationExclusionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestAutomationExclusionsTests
{
    public static IEnumerable<object[]> Policies()
    {
        for (var flags = 0; flags < 32; flags++)
        {
            yield return [(flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, (flags & 16) != 0];
        }
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public void PolicyOverridesNeverMutateBackendAssignments(bool preventSuppression, bool noRetry, bool quarantined, bool disabled, bool attemptToFix)
    {
        var assigned = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined, disabled, attemptToFix);
        var effective = new TestAutomationExclusions(preventSuppression, noRetry).ApplyTo(assigned);

        effective.Quarantined.Should().Be(quarantined && !preventSuppression);
        effective.Disabled.Should().Be(disabled && !preventSuppression);
        effective.AttemptToFix.Should().Be(attemptToFix && !noRetry);
        assigned.Quarantined.Should().Be(quarantined);
        assigned.Disabled.Should().Be(disabled);
        assigned.AttemptToFix.Should().Be(attemptToFix);
        Common.IsDisabledByTestManagement(effective).Should().Be(disabled && !preventSuppression && (!attemptToFix || noRetry));
    }

    [Theory]
    [InlineData(nameof(Declarations.Protected), true, true)]
    [InlineData(nameof(Declarations.ManualProtected), true, true)]
    [InlineData(nameof(Declarations.NoRetries), false, true)]
    [InlineData(nameof(Declarations.Unannotated), false, false)]
    public void ReadsAttributesFromBothPublicAssembliesWithoutSharingDeclarationState(string method, bool preventSuppression, bool noRetry)
    {
        var exclusions = TestAutomationExclusions.From(typeof(Declarations).GetMethod(method));
        exclusions.PreventFailureSuppression.Should().Be(preventSuppression);
        exclusions.NoAutoRetry.Should().Be(noRetry);
        TestAutomationExclusions.From(typeof(Declarations).GetMethod(nameof(Declarations.Unannotated))).NoAutoRetry.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, true, false, true, true)]
    public void ExclusionsRestoreOrdinaryTestImpactAnalysisSelection(bool quarantined, bool attemptToFix, bool preventSuppression, bool noRetry, bool expected)
    {
        var assigned = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined, false, attemptToFix);

        Common.CanApplyItrSkip(new TestAutomationExclusions(preventSuppression, noRetry).ApplyTo(assigned)).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, "fail")]
    [InlineData(false, true, "skip")]
    [InlineData(true, false, "fail")]
    [InlineData(true, true, "fail")]
    public void ProtectedFailureRemainsBlockingAndAssignmentsRemainReported(bool preventSuppression, bool noRetry, string expected)
    {
        var tags = new TestSpanTags { IsQuarantined = "true", IsDisabled = "true", IsAttemptToFix = "true" };
        new TestAutomationExclusions(preventSuppression, noRetry).ApplyTo(tags);

        Common.CalculateFinalStatus(false, true, false, tags).Should().Be(expected);
        tags.IsQuarantined.Should().Be("true");
        tags.IsDisabled.Should().Be("true");
        tags.IsAttemptToFix.Should().Be("true");
        tags.PreventFailureSuppression.Should().Be(preventSuppression ? "true" : null);
        tags.NoAutoRetry.Should().Be(noRetry ? "true" : null);
    }

    [Fact]
    public void NoAutoRetryDisablesAllXUnitRetryPolicies()
    {
        var optimization = new Mock<ITestOptimization>(MockBehavior.Strict);
        XUnitIntegration.SelectRetryMode(optimization.Object, testIsNew: true, isAttemptToFix: true, noAutoRetry: true).Should().Be(TestRetryMode.None);
        XUnitIntegration.SelectRetryMode(optimization.Object, testIsNew: true, isAttemptToFix: false, noAutoRetry: true).Should().Be(TestRetryMode.None);
    }

    [Fact]
    public void SettingSpanTagsDoesNotApplyExclusionPolicy()
    {
        var tags = new TestSpanTags
        {
            PreventFailureSuppression = "true",
            NoAutoRetry = "true",
            IsQuarantined = "true"
        };
        TestAutomationExclusions.From(tags).PreventFailureSuppression.Should().BeFalse();
        TestAutomationExclusions.From(tags).NoAutoRetry.Should().BeFalse();
        Common.IsFailureSuppressed(tags).Should().BeTrue();

        tags.IsAttemptToFix = "true";
        Common.IsAttemptToFix(tags).Should().BeTrue();
    }

    [Fact]
    public void ClearingSpanTagsDoesNotRemoveDeclaredExclusionPolicy()
    {
        var tags = new TestSpanTags { IsQuarantined = "true", IsAttemptToFix = "true" };
        new TestAutomationExclusions(true, true).ApplyTo(tags);
        tags.SetTag(TestTags.TestPreventFailureSuppression, null);
        tags.SetTag(TestTags.TestNoAutoRetry, null);

        TestAutomationExclusions.From(tags).PreventFailureSuppression.Should().BeTrue();
        TestAutomationExclusions.From(tags).NoAutoRetry.Should().BeTrue();
        Common.IsFailureSuppressed(tags).Should().BeFalse();
        Common.IsAttemptToFix(tags).Should().BeFalse();
        Common.CalculateFinalStatus(false, true, false, tags).Should().Be("fail");
    }

    [Fact]
    public void MetadataReadFailureFallsBackToAttributeInstances()
    {
        var method = new Mock<MethodInfo>();
        method.Setup(x => x.GetCustomAttributesData()).Throws(new FileNotFoundException());
        method.Setup(x => x.GetCustomAttributes(false)).Returns([new NoAutoRetryAttribute()]);

        var exclusions = TestAutomationExclusions.From(method.Object);

        exclusions.PreventFailureSuppression.Should().BeFalse();
        exclusions.NoAutoRetry.Should().BeTrue();
    }

    [Fact]
    public void UnreadableAttributesNeverEnableFailureSuppression()
    {
        var method = new Mock<MethodInfo>();
        method.Setup(x => x.GetCustomAttributesData()).Throws(new FileNotFoundException());
        method.Setup(x => x.GetCustomAttributes(false)).Throws(new FileNotFoundException());

        var exclusions = TestAutomationExclusions.From(method.Object);

        exclusions.PreventFailureSuppression.Should().BeTrue();
        exclusions.NoAutoRetry.Should().BeFalse();
    }

    [Fact]
    public void PublicAttributesApplyOnlyToIndividualMethods()
    {
        foreach (var attribute in new[] { typeof(NoAutoRetryAttribute), typeof(PreventFailureSuppressionAttribute), typeof(DatadogTraceManual::Datadog.Trace.Ci.NoAutoRetryAttribute), typeof(DatadogTraceManual::Datadog.Trace.Ci.PreventFailureSuppressionAttribute) })
        {
            var usage = attribute.GetCustomAttribute<AttributeUsageAttribute>();
            usage.ValidOn.Should().Be(AttributeTargets.Method);
            usage.Inherited.Should().BeFalse();
            usage.AllowMultiple.Should().BeFalse();
        }
    }

    [Fact]
    public void AttributesApplyToInheritedDeclarationsButNotOverrides()
    {
        var inherited = typeof(InheritedDeclarations).GetMethod(nameof(BaseDeclarations.Protected));
        var overridden = typeof(OverriddenDeclarations).GetMethod(nameof(BaseDeclarations.Protected));
        inherited.Should().NotBeNull();
        overridden.Should().NotBeNull();

        var inheritedExclusions = TestAutomationExclusions.From(inherited);
        inheritedExclusions.PreventFailureSuppression.Should().BeTrue();
        inheritedExclusions.NoAutoRetry.Should().BeTrue();

        var overriddenExclusions = TestAutomationExclusions.From(overridden);
        overriddenExclusions.PreventFailureSuppression.Should().BeFalse();
        overriddenExclusions.NoAutoRetry.Should().BeFalse();
    }

    [Fact]
    public void RemovingExclusionsOmitsTagsAndRestoresPolicy()
    {
        var tags = new TestSpanTags();
        new TestAutomationExclusions(true, true).ApplyTo(tags);
        default(TestAutomationExclusions).ApplyTo(tags);
        tags.PreventFailureSuppression.Should().BeNull();
        tags.NoAutoRetry.Should().BeNull();
        var assigned = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(true, true, true);
        TestAutomationExclusions.From(tags).ApplyTo(assigned).Should().BeSameAs(assigned);
    }

    private class BaseDeclarations
    {
        [PreventFailureSuppression]
        [NoAutoRetry]
        public virtual void Protected()
        {
        }
    }

    private sealed class InheritedDeclarations : BaseDeclarations
    {
    }

    private sealed class OverriddenDeclarations : BaseDeclarations
    {
        public override void Protected()
        {
        }
    }

    private sealed class Declarations
    {
        [PreventFailureSuppression]
        [NoAutoRetry]
        public void Protected()
        {
        }

        [DatadogTraceManual::Datadog.Trace.Ci.PreventFailureSuppression]
        [DatadogTraceManual::Datadog.Trace.Ci.NoAutoRetry]
        public void ManualProtected()
        {
        }

        [NoAutoRetry]
        public void NoRetries()
        {
        }

        public void Unannotated()
        {
        }
    }
}
