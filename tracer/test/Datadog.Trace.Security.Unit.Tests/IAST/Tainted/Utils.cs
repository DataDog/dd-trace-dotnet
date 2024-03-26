// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.Unit.Tests.Iast;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST.Tainted;

internal static class Utils
{
    public static EvidenceRedactor GetDefaultRedactor(double? timeoutMs = null)
    {
        var settingsDictionary = new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RedactionEnabled, true }
        };

        if (timeoutMs is not null)
        {
            settingsDictionary[ConfigurationKeys.Iast.RegexTimeout] = timeoutMs;
        }

        var settings = new CustomSettingsForTests(settingsDictionary);

        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);

        var evidenceRedactor = IastModule.CreateRedactor(iastSettings);
        Assert.NotNull(evidenceRedactor);
        return evidenceRedactor;
    }

    public static VulnerabilityBatch GetRedactedBatch(double? timeoutMs = null)
    {
        return new VulnerabilityBatch(IastSettings.TruncationMaxValueLengthDefault, GetDefaultRedactor(timeoutMs));
    }

    public static System.Func<string, string, bool> GetRegexScrubber(params string[] rules)
    {
        List<Regex> regexes = new List<Regex>();
        foreach (var rule in rules)
        {
            regexes.Add(new Regex(rule.Replace("[", "\\[").Replace("]", "\\]").Replace("$", "\\$").Replace(".", "\\.").Replace("*", ".*"), RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        return GetRegexScrubber(regexes.ToArray());
    }

    public static System.Func<string, string, bool> GetRegexScrubber(params Regex[] rules)
    {
        return (path, attr) =>
        {
            foreach (var rule in rules)
            {
                if (rule.IsMatch(path + "." + attr))
                {
                    return true;
                }
            }

            return false;
        };
    }
}
