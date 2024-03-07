// <copyright file="VerifyHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using VerifyTests;
using VerifyXunit;

namespace Datadog.Trace.TestHelpers
{
    public static class VerifyHelper
    {
        internal static readonly RegexOptions RegOptions = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        internal static readonly IEnumerable<(Regex RegexPattern, string Replacement)> SpanScrubbers = new List<(Regex RegexPattern, string Replacement)>
        {
            (new(@"localhost\:\d+", RegOptions), "localhost:00000"),
            // bytes differ slightly depending on platform
            (new(@"http.response.headers.content-length\: 2\d{3}", RegOptions), "http.response.headers.content-length: 2xxx"),
            (new(@"127.0.0.1\:\d+", RegOptions), "localhost:00000"),
            (new(@"_dd.tracer_kr: \d\.\d+", RegOptions), "_dd.tracer_kr: 1.0"),
            (new(@"process_id: \d+\.0", RegOptions), "process_id: 0"),
            (new(@"http.client_ip: (.)*(?=,)", RegOptions), "http.client_ip: 127.0.0.1"),
            (new(@"http.useragent: grpc-dotnet\/(.)*(?=,)", RegOptions), "http.useragent: grpc-dotnet/123"),
            (new(@"git.commit.sha: [0-9a-f]{40}", RegOptions), "git.commit.sha: aaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbb"),
            (new(@"_dd\.p\.tid: [0-9a-f]{16}", RegOptions), "_dd.p.tid: 1234567890abcdef"),
            (new("x-datadog-trace-id\":\\[\\[\\[8,({\"category\":\"pii\",\"type\":\"vin\"})\\]\\]", RegOptions), "x-datadog-trace-id\":[[[8]]") // api security, sometimes we can get "x-datadog-trace-id":[[[8,{"category":"pii","type":"vin"}]], and not everytime depending on the number, should be removed with waf 1.15.1, bug is fixed
        };

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

        public static void InitializeGlobalSettings()
        {
            VerifierSettings.DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                {
                    return new(directory: Path.Combine(projectDirectory, "..", "snapshots"));
                });
        }

        public static VerifySettings GetSpanVerifierSettings(params object[] parameters) => GetSpanVerifierSettings(null, parameters);

        public static VerifySettings GetCIVisibilitySpanVerifierSettings(params object[] parameters) => GetCIVisibilitySpanVerifierSettings(null, parameters);

        public static VerifySettings GetSpanVerifierSettings(IEnumerable<(Regex RegexPattern, string Replacement)> scrubbers, object[] parameters)
            => GetSpanVerifierSettings(scrubbers, parameters, ScrubTags, null);

        public static VerifySettings GetCIVisibilitySpanVerifierSettings(IEnumerable<(Regex RegexPattern, string Replacement)> scrubbers, object[] parameters)
            => GetSpanVerifierSettings(scrubbers, parameters, ScrubCIVisibilityTags, ScrubCIVisibilityTags);

        public static VerifySettings GetSpanVerifierSettings(IEnumerable<(Regex RegexPattern, string Replacement)> scrubbers, object[] parameters, ConvertMember<MockSpan, Dictionary<string, string>> tagsScrubber, ConvertMember<MockCIVisibilityTest, Dictionary<string, string>> metaScrubber)
        {
            var settings = new VerifySettings();

            InitializeGlobalSettings();

            if (parameters.Length > 0)
            {
                settings.UseParameters(parameters);
            }

            settings.ModifySerialization(_ =>
            {
                _.IgnoreMember<MockSpan>(s => s.Duration);
                _.IgnoreMember<MockSpan>(s => s.Start);
                _.MemberConverter<MockSpan, Dictionary<string, string>>(x => x.Tags, tagsScrubber);

                _.IgnoreMember<MockCIVisibilityTest>(s => s.Duration);
                _.IgnoreMember<MockCIVisibilityTest>(s => s.Start);
                if (metaScrubber is not null)
                {
                    _.MemberConverter<MockCIVisibilityTest, Dictionary<string, string>>(x => x.Meta, metaScrubber);
                }
            });

            foreach (var (regexPattern, replacement) in scrubbers ?? SpanScrubbers)
            {
                settings.AddRegexScrubber(regexPattern, replacement);
            }

            settings.ScrubInlineGuids();
            settings.ScrubEmptyLines();
            return settings;
        }

        public static SettingsTask VerifySpans(
            IReadOnlyCollection<MockSpan> spans,
            VerifySettings settings,
            Func<IReadOnlyCollection<MockSpan>, IOrderedEnumerable<MockSpan>> orderSpans = null)
        {
            // Ensure a static ordering for the spans
            var orderedSpans = orderSpans?.Invoke(spans) ??
                               spans
                                  .OrderBy(x => GetRootSpanResourceName(x, spans))
                                  .ThenBy(x => GetSpanDepth(x, spans))
                                  .ThenBy(x => x.Start)
                                  .ThenBy(x => x.Duration);

            return Verifier.Verify(orderedSpans, settings);
        }

        public static string GetRootSpanResourceName(MockSpan span, IReadOnlyCollection<MockSpan> allSpans)
        {
            while (span.ParentId is not null)
            {
                var parent = allSpans.FirstOrDefault(x => x.SpanId == span.ParentId.Value);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                span = parent;
            }

            return span.Resource;
        }

        public static int GetSpanDepth(MockSpan span, IReadOnlyCollection<MockSpan> allSpans)
        {
            var depth = 0;
            while (span.ParentId is not null)
            {
                var parent = allSpans.FirstOrDefault(x => x.SpanId == span.ParentId.Value);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                span = parent;
                depth++;
            }

            return depth;
        }

        public static void AddRegexScrubber(this VerifySettings settings, Regex regex, string replacement)
        {
            settings.AddScrubber(builder => ReplaceRegex(builder, regex, replacement));
        }

        public static void AddRegexScrubber(this VerifySettings settings, (Regex RegexPattern, string Replacement) scrubber)
        {
            settings.AddScrubber(builder => ReplaceRegex(builder, scrubber.RegexPattern, scrubber.Replacement));
        }

        public static void AddSimpleScrubber(this VerifySettings settings, string oldValue, string newValue)
        {
            settings.AddScrubber(builder => ReplaceSimple(builder, oldValue, newValue));
        }

        public static Dictionary<string, string> ScrubTags(MockSpan span, Dictionary<string, string> tags)
        {
            return tags
                  // remove propagated tags because their positions in the snapshots are not stable
                  // with our span ordering. correct position (first span in every trace chunk) is covered by other tests.
                  .Where(kvp => !kvp.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal))
                  // We must ignore both `_dd.git.repository_url` and `_dd.git.commit.sha` because we are only setting it on the first span of a trace
                  // no matter what. That means we have unstable snapshot results.
                  .Where(kvp => kvp.Key != Tags.GitRepositoryUrl && kvp.Key != Tags.GitCommitSha)
                  .Select(
                       kvp => kvp.Key switch
                       {
                           // scrub stack trace for errors
                           Tags.ErrorStack => new KeyValuePair<string, string>(kvp.Key, ScrubStackTrace(kvp.Value)),
                           _ => kvp
                       })
                  .OrderBy(x => x.Key)
                  .ToDictionary(x => x.Key, x => x.Value);
        }

        public static Dictionary<string, string> ScrubCIVisibilityTags(MockSpan span, Dictionary<string, string> tags) => ScrubCIVisibilityTags(tags);

        public static Dictionary<string, string> ScrubCIVisibilityTags(MockCIVisibilityTest span, Dictionary<string, string> tags) => ScrubCIVisibilityTags(tags);

        public static Dictionary<string, string> ScrubCIVisibilityTags(Dictionary<string, string> tags)
        {
            return tags
                  // remove propagated tags because their positions in the snapshots are not stable
                  // with our span ordering. correct position (first span in every trace chunk) is covered by other tests.
                  .Where(kvp => !kvp.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal))
                  // We must ignore both `_dd.git.repository_url` and `_dd.git.commit.sha` because we are only setting it on the first span of a trace
                  // no matter what. That means we have unstable snapshot results.
                  .Where(kvp => kvp.Key != Tags.GitRepositoryUrl && kvp.Key != Tags.GitCommitSha)
                  .Select(
                       kvp => kvp.Key switch
                       {
                           // scrub stack trace for errors
                           Tags.ErrorStack => new KeyValuePair<string, string>(kvp.Key, ScrubStackTrace(kvp.Value)),
                           Trace.Ci.Tags.CommonTags.GitBranch => new KeyValuePair<string, string>(kvp.Key, "branch"),
                           Trace.Ci.Tags.CommonTags.GitTag => new KeyValuePair<string, string>(kvp.Key, "tag"),
                           Trace.Ci.Tags.CommonTags.GitRepository => new KeyValuePair<string, string>(kvp.Key, "repository"),
                           Trace.Ci.Tags.CommonTags.GitCommit => new KeyValuePair<string, string>(kvp.Key, "aaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbb"),
                           Trace.Ci.Tags.CommonTags.GitCommitMessage => new KeyValuePair<string, string>(kvp.Key, "CommitMessage"),
                           Trace.Ci.Tags.CommonTags.GitCommitAuthorDate => new KeyValuePair<string, string>(kvp.Key, "AuthorDate"),
                           Trace.Ci.Tags.CommonTags.GitCommitAuthorEmail => new KeyValuePair<string, string>(kvp.Key, "author@email.com"),
                           Trace.Ci.Tags.CommonTags.GitCommitAuthorName => new KeyValuePair<string, string>(kvp.Key, "author name"),
                           Trace.Ci.Tags.CommonTags.GitCommitCommitterDate => new KeyValuePair<string, string>(kvp.Key, "CommitterDate"),
                           Trace.Ci.Tags.CommonTags.GitCommitCommitterEmail => new KeyValuePair<string, string>(kvp.Key, "committer@email.com"),
                           Trace.Ci.Tags.CommonTags.GitCommitCommitterName => new KeyValuePair<string, string>(kvp.Key, "committer name"),
                           Trace.Ci.Tags.TestTags.CodeOwners => new KeyValuePair<string, string>(kvp.Key, "[ \"@MyTeam\" ]"),
                           Trace.Ci.Tags.CommonTags.OSArchitecture => new KeyValuePair<string, string>(kvp.Key, "OSArchitecture"),
                           Trace.Ci.Tags.CommonTags.OSPlatform => new KeyValuePair<string, string>(kvp.Key, "OSPlatform"),
                           Trace.Ci.Tags.CommonTags.OSVersion => new KeyValuePair<string, string>(kvp.Key, "OSVersion"),
                           Trace.Ci.Tags.CommonTags.RuntimeArchitecture => new KeyValuePair<string, string>(kvp.Key, "RuntimeArchitecture"),
                           Trace.Ci.Tags.CommonTags.RuntimeName => new KeyValuePair<string, string>(kvp.Key, "RuntimeName"),
                           Trace.Ci.Tags.CommonTags.RuntimeVersion => new KeyValuePair<string, string>(kvp.Key, "RuntimeVersion"),
                           Trace.Ci.Tags.CommonTags.LibraryVersion => new KeyValuePair<string, string>(kvp.Key, "LibraryVersion"),
                           Trace.Ci.Tags.TestTags.Command => new KeyValuePair<string, string>(kvp.Key, "Command"),
                           Trace.Ci.Tags.TestTags.CommandWorkingDirectory => new KeyValuePair<string, string>(kvp.Key, "CommandWorkingDirectory"),
                           _ => kvp
                       })
                  .OrderBy(x => x.Key)
                  .ToDictionary(x => x.Key, x => x.Value);
        }

        public static void AddDataStreamsScrubber(this VerifySettings settings)
        {
            settings.ModifySerialization(
                _ =>
                {
                    _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(
                        x => x.EdgeLatency,
                        (_, v) => v?.Length == 0 ? v : new byte[] { 0xFF });
                    _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(
                        x => x.PathwayLatency,
                        (_, v) => v?.Length == 0 ? v : new byte[] { 0xFF });
                    _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(
                        x => x.PayloadSize,
                        (_, v) =>  v?.Length == 0 ? v : new byte[] { 0xFF });
                });
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
