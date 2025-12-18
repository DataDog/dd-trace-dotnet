// <copyright file="TestOptimizationDetection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// A helper class for detection of whether TestOptimization is enabled
/// </summary>
internal static class TestOptimizationDetection
{
    public static Enablement IsEnabled(IConfigurationSource source, IConfigurationTelemetry telemetry, IDatadogLogger log)
    {
        // By configuration
        var config = new ConfigurationBuilder(source, telemetry);
        var explicitlyEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.Enabled).AsBool();
        if (explicitlyEnabled is { } enabled)
        {
            if (enabled)
            {
                log.Information("TestOptimization: CI Visibility Enabled by Configuration");
            }
            else
            {
                log.Information("TestOptimization: CI Visibility Disabled by Configuration");
            }

            return new Enablement(enabled, false);
        }

        return new Enablement(null, InferredAvailable(log));
    }

    private static bool InferredAvailable(IDatadogLogger log)
    {
        // Try to autodetect based in the domain name.
        var domainName = AppDomain.CurrentDomain.FriendlyName ?? string.Empty;
        if (domainName.StartsWith("testhost", StringComparison.Ordinal) ||
            domainName.StartsWith("xunit", StringComparison.Ordinal) ||
            domainName.StartsWith("nunit", StringComparison.Ordinal) ||
            domainName.StartsWith("MSBuild", StringComparison.Ordinal))
        {
            log.Information("TestOptimization: CI Visibility Enabled by Domain name whitelist");
            return true;
        }

        // Try to autodetect based in the process name.
        var processName = GetProcessName(log);
        if (processName.StartsWith("testhost.", StringComparison.Ordinal))
        {
            log.Information("TestOptimization: CI Visibility Enabled by Process name whitelist");
            return true;
        }

        log.Debug("TestOptimization: CI Visibility Enabled by Domain name whitelist");
        return false;

        static string GetProcessName(IDatadogLogger log)
        {
            try
            {
                return ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception exception)
            {
                log.Warning(exception, "TestOptimization: Error getting current process name when checking CI Visibility status");
            }

            return string.Empty;
        }
    }

    public readonly record struct Enablement(bool? ExplicitEnabled, bool InferredEnabled)
    {
        public bool IsEnabled => ExplicitEnabled ?? InferredEnabled;
    }
}
