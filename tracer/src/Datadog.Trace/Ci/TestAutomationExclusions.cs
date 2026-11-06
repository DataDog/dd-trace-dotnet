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

    private readonly bool _preventFailureSuppression;
    private readonly bool _noAutoRetry;

    internal TestAutomationExclusions(bool preventFailureSuppression, bool noAutoRetry)
    {
        _preventFailureSuppression = preventFailureSuppression;
        _noAutoRetry = noAutoRetry;
    }

    internal bool AllowFailureSuppression => !_preventFailureSuppression;

    internal bool PreventFailureSuppression => _preventFailureSuppression;

    internal bool AllowAutoRetry => !_noAutoRetry;

    internal bool NoAutoRetry => _noAutoRetry;

    internal static TestAutomationExclusions From(MethodInfo? method)
    {
        if (method is null)
        {
            return default;
        }

        var preventFailureSuppression = false;
        var noAutoRetry = false;
        try
        {
            // Inspect metadata without constructing attributes or running customer code.
            foreach (var attribute in method.GetCustomAttributesData())
            {
                Read(attribute.AttributeType, ref preventFailureSuppression, ref noAutoRetry);
            }
        }
        catch (Exception ex)
        {
            // CustomAttributeData also resolves the member types of every attribute, so an unrelated attribute with a
            // missing dependency fails here even though the framework can still read it.
            Log.Debug(ex, "Unable to read test automation exclusion metadata, reading the attribute instances instead.");
            try
            {
                // Match by name because the manual and tracer assemblies define distinct attribute types.
                // IsDefined with the tracer type would miss the manual assembly; retain this fallback
                // when metadata inspection fails while resolving unrelated attribute dependencies.
                foreach (var attribute in method.GetCustomAttributes(inherit: false))
                {
                    Read(attribute.GetType(), ref preventFailureSuppression, ref noAutoRetry);
                }
            }
            catch (Exception fallbackEx)
            {
                Log.Error(fallbackEx, "Unable to read test automation exclusion attributes.");

                // An unknown declaration must not let Datadog suppress a failure.
                preventFailureSuppression = true;
            }
        }

        return new TestAutomationExclusions(preventFailureSuppression, noAutoRetry);
    }

    internal static TestAutomationExclusions From(TestSpanTags tags)
        => tags.AutomationExclusions;

    internal void ApplyTo(TestSpanTags tags)
    {
        tags.AutomationExclusions = this;
        tags.PreventFailureSuppression = !AllowFailureSuppression ? "true" : null;
        tags.NoAutoRetry = !AllowAutoRetry ? "true" : null;
    }

    internal TestOptimizationClient.TestManagementResponseTestPropertiesAttributes ApplyTo(TestOptimizationClient.TestManagementResponseTestPropertiesAttributes assigned)
        => !AllowFailureSuppression || !AllowAutoRetry
            ? new(assigned.Quarantined && AllowFailureSuppression, assigned.Disabled && AllowFailureSuppression, assigned.AttemptToFix && AllowAutoRetry)
            : assigned;

    private static void Read(Type attributeType, ref bool preventFailureSuppression, ref bool noAutoRetry)
    {
        switch (attributeType.FullName)
        {
            case $"Datadog.Trace.Ci.{nameof(PreventFailureSuppressionAttribute)}":
                preventFailureSuppression = true;
                break;
            case $"Datadog.Trace.Ci.{nameof(NoAutoRetryAttribute)}":
                noAutoRetry = true;
                break;
        }
    }
}
