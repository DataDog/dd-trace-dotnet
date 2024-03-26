// <copyright file="CIEnvironmentVariableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
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
                yield return new object[] { new JsonDataItem(name, jsonObject) };
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
            foreach (var testItem in jsonData.Data)
            {
                var envData = testItem[0];
                var spanData = testItem[1];

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

                    Assert.Equal(spanDataItem.Value, value);
                }
            }
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
