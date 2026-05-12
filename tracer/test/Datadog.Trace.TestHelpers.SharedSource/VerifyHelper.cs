// <copyright file="VerifyHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable  enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Tagging;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
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
                    new PathInfo(directory: Path.Combine(projectDirectory, "..", "snapshots")));
        }

        public static VerifySettings AddRegexScrubber(this VerifySettings settings, Regex regex, string replacement)
        {
            settings.AddScrubber(builder => ReplaceRegex(builder, regex, replacement));
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

        // Based on https://github.com/VerifyTests/Verify.DiffPlex/blob/9f9f2a18f35074680be47c9043e95d1857e457e0/src/Verify.DiffPlex/VerifyDiffPlex.cs
        public static class VerifyDiffPlex
        {
            /// <summary>
            /// OutputType.
            /// </summary>
            public enum OutputType
            {
                Full,
                Compact,
                Minimal
            }

            public static bool Initialized { get; private set; }

            public static void Initialize() => Initialize(OutputType.Full);

            public static void Initialize(OutputType outputType)
            {
                if (Initialized)
                {
                    throw new("Already Initialized");
                }

                Initialized = true;
                VerifierSettings.SetDefaultStringComparer((received, verified, _) => GetResult(outputType, received, verified));
            }

            public static void UseDiffPlex(VerifySettings settings, OutputType outputType = OutputType.Full) =>
                settings.UseStringComparer(
                    (received, verified, _) => GetResult(outputType, received, verified));

            public static SettingsTask UseDiffPlex(SettingsTask settings, OutputType outputType = OutputType.Full) =>
                settings.UseStringComparer(
                    (received, verified, _) => GetResult(outputType, received, verified));

            private static Func<string, string, StringBuilder> GetCompareFunc(OutputType outputType) =>
                outputType switch
                {
                    OutputType.Compact => CompactCompare,
                    OutputType.Minimal => MinimalCompare,
                    _ => VerboseCompare
                };

            private static Task<CompareResult> GetResult(OutputType outputType, string received, string verified)
            {
                var compare = GetCompareFunc(outputType);
                var builder = compare(received, verified);
                TrimEnd(builder);
                var message = builder.ToString();
                var result = CompareResult.NotEqual(message);
                return Task.FromResult(result);
            }

            private static StringBuilder VerboseCompare(string received, string verified)
            {
                var diff = InlineDiffBuilder.Diff(verified, received);

                var builder = new StringBuilder();
                foreach (var line in diff.Lines)
                {
                    switch (line.Type)
                    {
                        case ChangeType.Inserted:
                            builder.Append("+ ");
                            break;
                        case ChangeType.Deleted:
                            builder.Append("- ");
                            break;
                        default:
                            builder.Append("  ");
                            break;
                    }

                    builder.AppendLine(line.Text);
                }

                return builder;
            }

            private static StringBuilder MinimalCompare(string received, string verified)
            {
                var diff = InlineDiffBuilder.Diff(verified, received);

                var builder = new StringBuilder();
                foreach (var line in diff.Lines)
                {
                    switch (line.Type)
                    {
                        case ChangeType.Inserted:
                            builder.Append("+ ");
                            break;
                        case ChangeType.Deleted:
                            builder.Append("- ");
                            break;
                        default:
                            // omit unchanged files
                            continue;
                    }

                    builder.AppendLine(line.Text);
                }

                return builder;
            }

            private static StringBuilder CompactCompare(string received, string verified)
            {
                var diff = InlineDiffBuilder.Diff(verified, received);
                var builder = new StringBuilder();

                // ReSharper disable once RedundantSuppressNullableWarningExpression
                var prefixLength = diff.Lines.Max(_ => _.Position).ToString()!.Length;
                var spacePrefix = new string(' ', prefixLength - 1);

                static bool IsChanged(DiffPiece? line) => line?.Type is ChangeType.Inserted or ChangeType.Deleted;

                void AddDiffLine(int? lineNumber, string symbol, string text)
                {
                    var prefix = lineNumber?.ToString("D" + prefixLength) ?? spacePrefix + symbol;
                    builder.AppendLine($"{prefix} {text}");
                }

                DiffPiece? prevLine = null;
                var lastIndex = diff.Lines.Count - 1;

                for (var i = 0; i <= lastIndex; i++)
                {
                    var currentLine = diff.Lines[i];
                    var nextLine = i < lastIndex
                        ? diff.Lines[i + 1]
                        : null;

                    if (IsChanged(currentLine))
                    {
                        if (i == 0)
                        {
                            AddDiffLine(null, " ", "[BOF]");
                        }

                        var symbol = currentLine.Type == ChangeType.Inserted ? "+" : "-";
                        AddDiffLine(null, symbol, currentLine.Text);

                        if (i == lastIndex)
                        {
                            AddDiffLine(null, " ", "[EOF]");
                        }
                    }
                    else if (IsChanged(prevLine) || IsChanged(nextLine))
                    {
                        AddDiffLine(currentLine.Position, " ", currentLine.Text);
                        if (!IsChanged(nextLine))
                        {
                            builder.AppendLine();
                        }
                    }

                    prevLine = currentLine;
                }

                return builder;
            }

            private static void TrimEnd(StringBuilder builder)
            {
                if (builder.Length == 0)
                {
                    return;
                }

                var i = builder.Length - 1;
                for (; i >= 0; i--)
                {
                    if (!char.IsWhiteSpace(builder[i]))
                    {
                        break;
                    }
                }

                if (i < builder.Length - 1)
                {
                    builder.Length = i + 1;
                }
            }
        }
    }
}
