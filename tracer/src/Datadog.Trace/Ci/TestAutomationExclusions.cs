// <copyright file="TestAutomationExclusions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal readonly struct TestAutomationExclusions
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestAutomationExclusions>();

    internal TestAutomationExclusions(bool preventFailureSuppression, bool noAutoRetry)
    {
        PreventFailureSuppression = preventFailureSuppression;
        NoAutoRetry = noAutoRetry;
    }

    internal bool PreventFailureSuppression { get; }

    internal bool NoAutoRetry { get; }

    internal static TestAutomationExclusions From(MethodInfo? method)
    {
        var preventFailureSuppression = false;
        var noAutoRetry = false;
        try
        {
            if (method is not null)
            {
                // Inspect metadata without constructing attributes or running customer code.
                foreach (var attribute in method.GetCustomAttributesData())
                {
                    switch (attribute.AttributeType.FullName)
                    {
                        case "Datadog.Trace.Ci.PreventFailureSuppressionAttribute":
                            preventFailureSuppression = true;
                            break;
                        case "Datadog.Trace.Ci.NoAutoRetryAttribute":
                            noAutoRetry = true;
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to read test automation exclusion attributes.");
        }

        return new TestAutomationExclusions(preventFailureSuppression, noAutoRetry);
    }

    internal static TestAutomationExclusions From(TestSpanTags tags)
        => tags.AutomationExclusions;

    internal void ApplyTo(TestSpanTags tags)
    {
        tags.AutomationExclusions = this;
        tags.PreventFailureSuppression = PreventFailureSuppression ? "true" : null;
        tags.NoAutoRetry = NoAutoRetry ? "true" : null;
    }

    internal TestOptimizationClient.TestManagementResponseTestPropertiesAttributes ApplyTo(TestOptimizationClient.TestManagementResponseTestPropertiesAttributes assigned)
        => PreventFailureSuppression || NoAutoRetry
            ? new(assigned.Quarantined && !PreventFailureSuppression, assigned.Disabled && !PreventFailureSuppression, assigned.AttemptToFix && !NoAutoRetry)
            : assigned;
}
