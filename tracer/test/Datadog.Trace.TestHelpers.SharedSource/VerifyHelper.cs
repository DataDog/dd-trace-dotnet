// <copyright file="VerifyHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VerifyTests;
using VerifyXunit;

namespace Datadog.Trace.TestHelpers
{
    public static class VerifyHelper
    {
        private static readonly Regex LocalhostRegex = new(@"localhost\:\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LoopBackRegex = new(@"127.0.0.1\:\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex KeepRateRegex = new(@"_dd.tracer_kr: \d\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ProcessIdRegex = new(@"process_id: \d+\.0", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// With <see cref="Verify"/>, parameters are used as part of the filename.
        /// This method produces a "sanitised" version to remove problematic values
        /// </summary>
        /// <param name="path">The path to sanitise</param>
        /// <returns>The sanitised path</returns>
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

            VerifierSettings.DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                {
                    return new(directory: Path.Combine(projectDirectory, "..", "snapshots"));
                });

            if (parameters.Length > 0)
            {
                settings.UseParameters(parameters);
            }

            settings.ModifySerialization(_ =>
            {
                _.IgnoreMember<MockSpan>(s => s.Duration);
                _.IgnoreMember<MockSpan>(s => s.Start);
                _.MemberConverter<MockSpan, Dictionary<string, string>>(x => x.Tags, ScrubStackTraceForErrors);
            });
            settings.AddRegexScrubber(LocalhostRegex, "localhost:00000");
            settings.AddRegexScrubber(LoopBackRegex, "localhost:00000");
            settings.AddRegexScrubber(KeepRateRegex, "_dd.tracer_kr: 1.0");
            settings.AddRegexScrubber(ProcessIdRegex, "process_id: 0");
            return settings;
        }

        public static SettingsTask VerifySpans(IReadOnlyCollection<MockSpan> spans, VerifySettings settings)
        {
            // Ensure a static ordering for the spans
            var orderedSpans = spans
                              .OrderBy(x => GetRootSpanName(x, spans))
                              .ThenBy(x => GetSpanDepth(x, spans))
                              .ThenBy(x => x.Start)
                              .ThenBy(x => x.Duration);

            return Verifier.Verify(orderedSpans, settings);

            static string GetRootSpanName(MockSpan span, IReadOnlyCollection<MockSpan> allSpans)
            {
                while (span.ParentId is not null)
                {
                    span = allSpans.First(x => x.SpanId == span.ParentId.Value);
                }

                return span.Resource;
            }

            static int GetSpanDepth(MockSpan span, IReadOnlyCollection<MockSpan> allSpans)
            {
                var depth = 0;
                while (span.ParentId is not null)
                {
                    span = allSpans.First(x => x.SpanId == span.ParentId.Value);
                    depth++;
                }

                return depth;
            }
        }

        public static void AddRegexScrubber(this VerifySettings settings, Regex regex, string replacement)
        {
            settings.AddScrubber(builder => ReplaceRegex(builder, regex, replacement));
        }

        public static void AddSimpleScrubber(this VerifySettings settings, string oldValue, string newValue)
        {
            settings.AddScrubber(builder => ReplaceSimple(builder, oldValue, newValue));
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

        private static void ReplaceSimple(StringBuilder builder, string oldValue, string newValue)
        {
            var value = builder.ToString();
            var result = value.Replace(oldValue, newValue);

            if (value.Equals(result, StringComparison.Ordinal))
            {
                return;
            }

            builder.Clear();
            builder.Append(result);
        }

        private static Dictionary<string, string> ScrubStackTraceForErrors(
            MockSpan span, Dictionary<string, string> tags)
        {
            return tags
                  .Select(
                       kvp => kvp.Key switch
                       {
                           Tags.ErrorStack => new KeyValuePair<string, string>(kvp.Key, ScrubStackTrace(kvp.Value)),
                           _ => kvp
                       })
                  .OrderBy(x => x.Key)
                  .ToDictionary(x => x.Key, x => x.Value);
        }

        private static string ScrubStackTrace(string stackTrace)
        {
            // keep the message + the first (scrubbed) location
            var sb = new StringBuilder();
            using StringReader reader = new(Scrubbers.ScrubStackTrace(stackTrace));
            string line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith("at "))
                {
                    // add the first line of the stack trace
                    sb.Append(line);
                    break;
                }

                sb
                   .Append(line)
                   .Append('\n');
            }

            return sb.ToString();
        }
    }
}
