// <copyright file="VerifyHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using VerifyTests;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    internal static class VerifyHelper
    {
        public const string SnapshotDirectory = "Snapshots";
        private static readonly Regex LocalhostRegex = new(@"localhost\:\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex KeepRateRegex = new(@"_dd.tracer_kr: \d\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// With <see cref="Verify"/>, parameters are used as part of the filename.
        /// This method produces a "sanitised" version to remove problematic values
        /// </summary>
        public static string SanitisePathsForVerify(string path)
        {
            // TODO: Make this more robust
            return path
                  .Replace(@"\", "_")
                  .Replace("/", "_")
                  .Replace("?", "-");
        }

        public static VerifySettings GetSpanVerifierSettings(params object[] parameters)
        {
            var settings = new VerifySettings();
            settings.UseDirectory(SnapshotDirectory);
            if (parameters.Length > 0)
            {
                settings.UseParameters(parameters);
            }

            settings.ModifySerialization(_ =>
            {
                _.IgnoreMember<MockTracerAgent.Span>(s => s.Duration);
                _.IgnoreMember<MockTracerAgent.Span>(s => s.Start);
                _.MemberConverter<MockTracerAgent.Span, Dictionary<string, string>>(x => x.Tags, ScrubStackTraceForErrors);
            });
            settings.AddScrubber(builder => ReplaceRegex(builder, LocalhostRegex, "localhost:00000"));
            settings.AddScrubber(builder => ReplaceRegex(builder, KeepRateRegex, "_dd.tracer_kr: 1.0"));
            return settings;
        }

        private static void ReplaceRegex(StringBuilder builder, Regex regex, string replacement)
        {
            var value = builder.ToString();
            var result = regex.Replace(value, replacement);

            if (value.Equals(result, StringComparison.Ordinal))
            {
                return;
            }

            builder.Clear();
            builder.Append(result);
        }

        private static Dictionary<string, string> ScrubStackTraceForErrors(
            MockTracerAgent.Span span, Dictionary<string, string> tags)
        {
            return tags
                  .Select(
                       kvp => kvp.Key switch
                       {
                           Tags.ErrorStack => new KeyValuePair<string, string>(kvp.Key, Scrubbers.ScrubStackTrace(kvp.Value)),
                           _ => kvp
                       })
                  .OrderBy(x => x.Key)
                  .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
#endif
