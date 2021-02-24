using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
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
                yield return new object[] { new JsonDataItem(name, jsonObject) };
            }
        }

        [Theory]
        [MemberData(nameof(GetJsonItems))]
        public void CheckEnvironmentVariables(JsonDataItem jsonData)
        {
            SpanContext context = new SpanContext(null, null, null);
            DateTimeOffset time = DateTimeOffset.UtcNow;
            foreach (Dictionary<string, string>[] testItem in jsonData.Data)
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
                Assert.Equal(jsonData.Name, providerName);
            }
        }

        internal void SetEnvironmentFromDictionary(IDictionary values)
        {
            foreach (DictionaryEntry item in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), null);
            }

            foreach (DictionaryEntry item in values)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), item.Value.ToString());
            }
        }

        internal void ResetEnvironmentFromDictionary(IDictionary values)
        {
            foreach (DictionaryEntry item in values)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), null);
            }

            foreach (DictionaryEntry item in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(item.Key.ToString(), item.Value.ToString());
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
