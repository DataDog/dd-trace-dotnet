// <copyright file="CIEnvironmentVariableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [CollectionDefinition(nameof(CIEnvironmentVariableTests), DisableParallelization = true)]
    [Collection(nameof(CIEnvironmentVariableTests))]
    public class CIEnvironmentVariableTests
    {
        private IDictionary _originalEnvVars;

        public CIEnvironmentVariableTests()
        {
            _originalEnvVars = Environment.GetEnvironmentVariables();
        }

        public static IEnumerable<object[]> GetJsonItems()
        {
            // Check if the CI\Data folder exists.
            var jsonFolder = DataHelpers.GetCiDataDirectory();

            // JSON file path
            foreach (var filePath in Directory.EnumerateFiles(jsonFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                var content = File.ReadAllText(filePath);
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, string>[][]>(content);
                if (jsonObject is not null)
                {
                    yield return [new JsonDataItem(name, jsonObject)];
                }
            }
        }

        [InlineData("git@github.com:DataDog/dd-trace-dotnet.git", true)]
        [InlineData("git@git@hub.com:DataDog/dd-trace-dotnet.git", false)]
        [InlineData("git@git/hub.com:DataDog/dd-trace-dotnet.git", false)]
        [InlineData("git@git$hub.com:DataDog/dd-trace-dotnet.git", false)]
        [InlineData("github.com:DataDog/dd-trace-dotnet.git", false)]
        [InlineData("https://github.com/DataDog/dd-trace-dotnet.git", true)]
        [InlineData("https://github.com/DataDog/dd-trace-dotnet", true)]
        [InlineData("git@gitlab.com:gitlab-org/gitlab.git", true)]
        [InlineData("https://gitlab.com/gitlab-org/gitlab.git", true)]
        [SkippableTheory]
        public void RepositoryPattern(string value, bool expected)
        {
            Assert.Equal(expected, Regex.Match(value, CIEnvironmentValues.RepositoryUrlPattern).Length == value.Length);
        }

        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("Hello World", "Hello World")]
        [InlineData("user@host", "user@host")]
        [InlineData("https://username@github.com/username/repository.git", "https://github.com/username/repository.git")]
        [InlineData("https://username:password@github.com/username/repository.git", "https://github.com/username/repository.git")]
        [InlineData("user@host:path/to/repo", "user@host:path/to/repo")]
        [InlineData("ssh://user@host:path/to/repo", "ssh://host:path/to/repo")]
        [InlineData("ssh://user@host:23/path/to/repo", "ssh://host:23/path/to/repo")]
        [InlineData("ftp://user@host:23/path/to/repo", "ftp://host:23/path/to/repo")]
        [SkippableTheory]
        public void CleanSensitiveDataFromRepositoryUrl(string value, string expected)
        {
            CIEnvironmentValues.RemoveSensitiveInformationFromUrl(value).Should().Be(expected);
        }

        [SkippableTheory]
        [MemberData(nameof(GetJsonItems))]
        public void CheckEnvironmentVariables(JsonDataItem jsonData)
        {
            var context = new SpanContext(null, null, null);
            var time = DateTimeOffset.UtcNow;
            var testIndex = -1;
            foreach (var testItem in jsonData.Data)
            {
                var envData = testItem[0];
                var spanData = testItem[1];
                testIndex++;

                var span = new Span(context, time);
                CIEnvironmentValues.Create(envData).DecorateSpan(span);

                foreach (var spanDataItem in spanData)
                {
                    var value = span.Tags.GetTag(spanDataItem.Key);

                    /* Due date parsing and DateTimeOffset.ToString() we need to remove
                     * The fraction of a second part from the actual value.
                     *     Expected: 2021-07-21T11:43:07-04:00
                     *     Actual:   2021-07-21T11:43:07.000-04:00
                     */
                    if (spanDataItem.Key.Contains(".date"))
                    {
                        if (jsonData.Name == "usersupplied")
                        {
                            // We cannot compare dates on the usersupplied json file.
                            continue;
                        }

                        if (spanDataItem.Value.Contains("usersupplied"))
                        {
                            // We cannot parse non datetime data.
                            continue;
                        }

                        value = value.Replace(".000", string.Empty);
                    }

                    if (spanDataItem.Key == CommonTags.CINodeLabels)
                    {
                        var labelsExpected = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(
                            spanDataItem.Value);
                        Array.Sort(labelsExpected);

                        var labelsActual =
                            Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(value);
                        Array.Sort(labelsActual);

                        labelsActual.Should().Equal(labelsExpected);
                        continue;
                    }

                    value.Should().Be(spanDataItem.Value, $"for {jsonData.Name}[{testIndex}]\\{spanDataItem.Key}");
                }
            }
        }

        [SkippableFact]
        public void GithubEventJsonTest()
        {
            var reloadEnvironmentData = typeof(CIEnvironmentValues).GetMethod("ReloadEnvironmentData", BindingFlags.Instance | BindingFlags.NonPublic);

            // Check if the CI\Data folder exists.
            var ciDataFolder = DataHelpers.GetCiDataDirectory();

            // JSON file path
            var jsonFile = Path.Combine(ciDataFolder, "githubevent", "github-event.json");

            // Let's test the github-event.json load and check the values first.
            var githubEnvVars = new GithubActionsEnvironmentValues<DictionaryValuesProvider>(
                new DictionaryValuesProvider(
                    new Dictionary<string, string>
                    {
                        [PlatformKeys.Ci.GitHub.EventPath] = jsonFile,
                    }));

            reloadEnvironmentData?.Invoke(githubEnvVars, null);
            githubEnvVars.HeadCommit.Should().Be("df289512a51123083a8e6931dd6f57bb3883d4c4");
            githubEnvVars.PrBaseHeadCommit.Should().Be("52e0974c74d41160a03d59ddc73bb9f5adab054b");
            githubEnvVars.PrBaseBranch.Should().Be("main");
            githubEnvVars.PrNumber.Should().Be("1");

            // Let's test now the `GITHUB_BASE_REF` environment variable.
            githubEnvVars = new GithubActionsEnvironmentValues<DictionaryValuesProvider>(
                new DictionaryValuesProvider(
                    new Dictionary<string, string>
                    {
                        [PlatformKeys.Ci.GitHub.BaseRef] = "my-custom-branch",
                    }));

            reloadEnvironmentData?.Invoke(githubEnvVars, null);
            githubEnvVars.PrBaseBranch.Should().Be("my-custom-branch");
        }

        /*
         *  Test matrix
         *  ───────────
         *  1.   refs/heads/tags/<tag>     → Group 1
         *  2.   refs/heads/<branch>       → Group 2
         *  3.   refs/tags/<tag>           → Group 3
         *  4.   refs/<anything>           → Group 4
         *  5.   origin/tags/<tag>         → Group 5
         *  6.   origin/<branch>           → Group 6  (not surfaced by the method – returns “ ”)
         *  7.   Empty / null input        → Pass-through
         *  8.   String that doesn’t match → Pass-through
         */

        [Theory]
        // refs/heads/tags/…
        [InlineData("refs/heads/tags/v1.0.0",         "v1.0.0")]
        [InlineData("refs/heads/tags/release-2025",   "release-2025")]

        // refs/heads/…
        [InlineData("refs/heads/main",                "main")]
        [InlineData("refs/heads/feature/add-login",   "feature/add-login")]

        // refs/tags/…
        [InlineData("refs/tags/v2.3.4",               "v2.3.4")]

        // refs/…
        [InlineData("refs/release/2025-07-21",        "release/2025-07-21")]

        // origin/tags/…
        [InlineData("origin/tags/v3.0.0-beta",        "v3.0.0-beta")]

        // origin/…   (group 6 → not surfaced, so output is empty string)
        [InlineData("origin/hotfix-42",               "")]

        // Edge & negative cases
        [InlineData("",                              "")]          // empty input
        [InlineData(null,                            null)]         // null input
        [InlineData("foo/bar",                       "foo/bar")]    // pattern not matched → passthrough
        public void CleanTagValue_Should_Return_Expected_Result(string input, string expected)
        {
            var actual = CIEnvironmentValues.CleanTagValue(input);
            actual.Should().Be(expected);
        }

        /*
         *  Extraction rules recap
         *  ──────────────────────
         *  Branch  ← first non-empty of groups 2, 4, 6
         *  Tag     ← first non-empty of groups 1, 3, 5
         *
         *  Test matrix
         *  ───────────
         *  1. refs/heads/tags/<tag>      → tag only
         *  2. refs/heads/<branch>        → branch only
         *  3. refs/tags/<tag>            → tag only
         *  4. refs/<anything>            → branch only
         *  5. origin/tags/<tag>          → tag only
         *  6. origin/<branch>            → branch only
         *  7. Empty / null input         → pass-through
         *  8. Pattern miss               → pass-through (branch untouched, tag null)
         */

        [Theory]
        // refs/heads/tags/…  (group 1)
        [InlineData("refs/heads/tags/v1.0.0",            "",               "v1.0.0")]
        [InlineData("refs/heads/tags/release-2025",      "",               "release-2025")]

        // refs/heads/…        (group 2)
        [InlineData("refs/heads/main",                   "main",          "")]
        [InlineData("refs/heads/feature/add-login",      "feature/add-login", "")]

        // refs/tags/…          (group 3)
        [InlineData("refs/tags/v2.3.4",                  "",               "v2.3.4")]

        // refs/…               (group 4)
        [InlineData("refs/release/2025-07-21",           "release/2025-07-21", "")]

        // origin/tags/…        (group 5)
        [InlineData("origin/tags/v3.0.0-beta",           "",               "v3.0.0-beta")]

        // origin/…             (group 6)
        [InlineData("origin/hotfix-42",                  "hotfix-42",     "")]

        // Edge & negative cases
        [InlineData("",                                   "",              null)]    // empty input
        [InlineData(null,                                 null,            null)]    // null input
        [InlineData("foo/bar",                            "foo/bar",       null)]    // pattern not matched
        public void CleanBranchValue_Should_Return_Expected(string input, string expectedBranch, string expectedTag)
        {
            var result = CIEnvironmentValues.CleanBranchValue(input);
            result.Item1.Should().Be(expectedBranch);   // branch
            result.Item2.Should().Be(expectedTag);      // tag
        }

        public class JsonDataItem : IXunitSerializable
        {
            public JsonDataItem()
            {
            }

            internal JsonDataItem(string name, Dictionary<string, string>[][] data)
            {
                Name = name;
                Data = data;
            }

            internal string Name { get; private set; }

            internal Dictionary<string, string>[][] Data { get; private set; }

            private string DataString
            {
                get => JsonConvert.SerializeObject(Data) ?? string.Empty;
                set
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        Data = JsonConvert.DeserializeObject<Dictionary<string, string>[][]>(value);
                    }
                }
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                Name = info.GetValue<string>(nameof(Name));
                DataString = info.GetValue<string>(nameof(Data));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Name), Name);
                info.AddValue(nameof(Data), DataString);
            }

            public override string ToString() => $"Name={Name}";
        }
    }
}
