using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [CollectionDefinition(nameof(CIEnvironmentVariableTests), DisableParallelization = true)]
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
            string jsonFolder = Path.Combine("CI", "Data");
            if (!Directory.Exists(jsonFolder))
            {
                throw new DirectoryNotFoundException(jsonFolder);
            }

            // JSON file path
            foreach (string filePath in Directory.EnumerateFiles(jsonFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                string content = File.ReadAllText(filePath);
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, string>[][]>(content);
                yield return new object[] { name, jsonObject };
            }
        }

        [Theory]
        [MemberData(nameof(GetJsonItems))]
        public void CheckEnvironmentVariables(string name, Dictionary<string, string>[][] data)
        {
            SpanContext context = new SpanContext(null, null, null);
            DateTimeOffset time = DateTimeOffset.UtcNow;
            foreach (Dictionary<string, string>[] testItem in data)
            {
                Dictionary<string, string> envData = testItem[0];
                Dictionary<string, string> spanData = testItem[1];

                Span span = new Span(context, time);

                SetEnvironmentFromDictionary(envData);
                CIEnvironmentValues.ReloadEnvironmentData();
                CIEnvironmentValues.DecorateSpan(span);
                ResetEnvironmentFromDictionary(envData);

                foreach (KeyValuePair<string, string> spanDataItem in spanData)
                {
                    string value = span.Tags.GetTag(spanDataItem.Key);
                    Assert.Equal(spanDataItem.Value, value);
                }

                string providerName = span.Tags.GetTag(CommonTags.CIProvider);
                Assert.Equal(name, providerName);
            }
        }

        internal static void SetEnvironmentFromDictionary(IDictionary values)
        {
            foreach (DictionaryEntry item in values)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), item.Value.ToString());
            }
        }

        internal void ResetEnvironmentFromDictionary(IDictionary values)
        {
            foreach (DictionaryEntry item in values)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), string.Empty);
            }

            foreach (DictionaryEntry item in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), item.Value.ToString());
            }
        }
    }
}
