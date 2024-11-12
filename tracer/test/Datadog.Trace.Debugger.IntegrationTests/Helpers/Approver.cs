// <copyright file="Approver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.Helpers
{
    internal static class Approver
    {
        private static readonly string[] _typesToScrub = { nameof(IntPtr), nameof(Guid) };
        private static readonly string[] _knownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "lineNumber", "thread_name", "thread_id", "<>t__builder", "s_taskIdCounter", "<>u__1", "stack", "m_task" };
        private static readonly string[] _knownPropertiesToRemove = { "CachedReusableFilters", "MaxStateDepth", "MaxValidationDepth" };

        internal static async Task ApproveSnapshots(string[] snapshots, string testName, ITestOutputHelper output, string[] knownPropertiesToReplace = null, string[] knownPropertiesToRemove = null, bool orderPostScrubbing = false)
        {
            await ApproveOnDisk(snapshots, testName, "snapshots", output, knownPropertiesToReplace, knownPropertiesToRemove, orderPostScrubbing);
        }

        internal static async Task ApproveStatuses(string[] statuses, string testName, ITestOutputHelper output, string[] knownPropertiesToReplace = null, string[] knownPropertiesToRemove = null, bool orderPostScrubbing = false)
        {
            await ApproveOnDisk(statuses, testName, "statuses", output, knownPropertiesToReplace, knownPropertiesToRemove, orderPostScrubbing);
        }

        private static async Task ApproveOnDisk(string[] dataToApprove, string testName, string path, ITestOutputHelper output, string[] knownPropertiesToReplace = null, string[] knownPropertiesToRemove = null, bool orderPostScrubbing = false)
        {
            knownPropertiesToRemove ??= _knownPropertiesToRemove;
            knownPropertiesToReplace ??= _knownPropertiesToReplace;

            if (dataToApprove.Length > 1)
            {
                // Order the snapshots alphabetically so we'll be able to create deterministic approvals
                dataToApprove = dataToApprove.OrderBy(snapshot => snapshot).ToArray();
            }

            var settings = new VerifySettings();

            settings.UseFileName(testName);
            settings.DisableRequireUniquePrefix();

            settings.AddScrubber(ScrubSnapshotJson);

            settings.ScrubEmptyLines();

            foreach (var (regexPattern, replacement) in VerifyHelper.SpanScrubbers)
            {
                settings.AddRegexScrubber(regexPattern, replacement);
            }

            AddRuntimeIdScrubber(settings);

            VerifierSettings.DerivePathInfo(
                (_, projectDirectory, _, _) => new(directory: Path.Combine(projectDirectory, "Approvals", path)));

            var toVerify =
                "["
               +
                string.Join(
                    ",",
                    dataToApprove.Select(JsonUtility.NormalizeJsonString))
               +
                "]";

            await Verifier.Verify(NormalizeLineEndings(toVerify), settings);

            void ScrubSnapshotJson(StringBuilder input)
            {
                var json = JArray.Parse(input.ToString());

                var toRemove = new List<JToken>();

                foreach (var descendant in json.DescendantsAndSelf().OfType<JObject>())
                {
                    foreach (var item in descendant)
                    {
                        try
                        {
                            if (knownPropertiesToReplace.Contains(item.Key) && item.Value != null)
                            {
                                item.Value.Replace(JToken.FromObject("ScrubbedValue"));
                                continue;
                            }

                            if (knownPropertiesToRemove.Contains(item.Key) && item.Value != null)
                            {
                                toRemove.Add(item.Value.Parent);
                                continue;
                            }

                            var value = item.Value.ToString();
                            switch (item.Key)
                            {
                                case "type":
                                    // Sanitizes types whose values may vary from run to run and consequently produce a different approval file.
                                    if (_typesToScrub.Contains(item.Value.ToString()))
                                    {
                                        item.Value.Parent.Parent["value"].Replace("ScrubbedValue");
                                    }

                                    break;
                                case "function":

                                    // Remove stackframes from "System" namespace, or where the frame was not resolved to a method
                                    if (value.StartsWith("System") || value == string.Empty)
                                    {
                                        toRemove.Add(item.Value.Parent.Parent);
                                        continue;
                                    }

                                    // Scrub MoveNext methods from `stack` in the snapshot as it varies between Windows/Linux.
                                    if (value.Contains(".MoveNext"))
                                    {
                                        item.Value.Replace(string.Empty);
                                    }

                                    // Scrub generated DisplayClass from stack in the snapshot as it varies between .net frameworks
                                    if (value.Contains("<>c__DisplayClass"))
                                    {
                                        item.Value.Replace(string.Empty);
                                    }

                                    break;
                                case "fileName":
                                case "file":
                                    // Remove the full path of file names
                                    item.Value.Replace(Path.GetFileName(value));

                                    break;

                                case "message":
                                    if (!value.Contains("Installed probe ") && !value.Contains("Error installing probe ") && !value.Contains("Emitted probe ") &&
                                        !IsParentName(item, parentName: "throwable") &&
                                        !IsParentName(item, parentName: "exception"))
                                    {
                                        // remove snapshot message (not probe status)
                                        item.Value.Replace("ScrubbedValue");
                                    }

                                    break;

                                case "expr":
                                    if (value.StartsWith("Convert("))
                                    {
                                        var stringToRemove = ", IConvertible";
                                        var newValue = value.Replace(stringToRemove, string.Empty);
                                        item.Value.Replace(newValue);
                                    }

                                    break;

                                case "stacktrace":
                                    if (IsParentName(item, parentName: "throwable"))
                                    {
                                        // take only the first frame of the exception stacktrace
                                        var firstChild = item.Value.Children().FirstOrDefault();
                                        if (firstChild != null)
                                        {
                                            item.Value.Replace(new JArray(firstChild));
                                        }
                                    }

                                    break;

                                case "StackTrace":
                                    if (IsParentName(item, parentName: ".@exception.fields"))
                                    {
                                        item.Value.Replace("ScrubbedValue");
                                    }

                                    break;
                            }
                        }
                        catch (Exception)
                        {
                            output.WriteLine($"Failed to sanitize snapshot. The part we are trying to sanitize: {item}");
                            output.WriteLine($"Complete snapshot: {json}");

                            throw;
                        }

                        static bool IsParentName(KeyValuePair<string, JToken> item, string parentName)
                        {
                            return item.Value.Path.Substring(0, item.Value.Path.Length - $".{item.Key}".Length).EndsWith(parentName);
                        }
                    }
                }

                foreach (var itemToRemove in toRemove)
                {
                    var parent = itemToRemove.Parent;
                    itemToRemove.Remove();
                    RemoveEmptyParent(parent);
                }

                if (orderPostScrubbing)
                {
                    // Order the snapshots alphabetically so we'll be able to create deterministic approvals
                    json = new JArray(json.OrderBy(obj => obj.ToString()));
                }

                input.Clear().Append(json);
            }

            string NormalizeLineEndings(string text) =>
                text
                   .Replace(@"\r\n", @"\n")
                   .Replace(@"\n\r", @"\n")
                   .Replace(@"\r", @"\n")
                   .Replace(@"\n", @"\r\n");
        }

        private static void RemoveEmptyParent(JToken token)
        {
            if (token.Parent is JProperty && token is JObject obj && !obj.Properties().Any())
            {
                token.Parent.Remove();
            }
        }

        private static void AddRuntimeIdScrubber(VerifySettings settings)
        {
            var runtimeIdPattern = new Regex(@"(""runtimeId"": "")[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
            settings.AddRegexScrubber(runtimeIdPattern, "$1scrubbed");
        }
    }
}
