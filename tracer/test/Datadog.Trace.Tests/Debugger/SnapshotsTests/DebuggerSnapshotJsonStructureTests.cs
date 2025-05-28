// <copyright file="DebuggerSnapshotJsonStructureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotJsonStructureTests
    {
        static DebuggerSnapshotJsonStructureTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        [Fact]
        public void JsonStructure_ShouldBeComplete_WhenSerializationIsInterrupted()
        {
            // Test the most critical scenario: JSON structure completeness
            var problematicObject = new MultipleIssuesObject();
            var snapshot = SnapshotBuilder.GenerateSnapshot(problematicObject);

            var json = ValidateJsonStructure(snapshot);

            // Verify essential JSON structure is present and complete
            Assert.NotNull(json["debugger"]);
            Assert.NotNull(json["debugger"]["snapshot"]);

            // Verify captures section exists and is properly structured
            var captures = json["debugger"]["snapshot"]["captures"];
            Assert.NotNull(captures);

            // If return section exists, it should be properly closed
            var returnSection = captures["return"];
            if (returnSection != null)
            {
                // Verify return section has proper structure
                Assert.True(returnSection is JObject, "Return section should be a complete JSON object");
            }

            // Verify that even if serialization was cut short, the JSON is well-formed
            // This is the most important test - malformed JSON would fail parsing above

            // Additional validation: check for unclosed JSON structures
            var jsonString = snapshot.ToString();

            // Count opening and closing braces/brackets to ensure they match
            var openBraces = jsonString.Count(c => c == '{');
            var closeBraces = jsonString.Count(c => c == '}');
            var openBrackets = jsonString.Count(c => c == '[');
            var closeBrackets = jsonString.Count(c => c == ']');

            Assert.Equal(openBraces, closeBraces);
            Assert.Equal(openBrackets, closeBrackets);

            // Verify the JSON ends properly (not truncated mid-structure)
            Assert.True(jsonString.TrimEnd().EndsWith("}"), "JSON should end with a closing brace");
        }

        [Fact]
        public void LimitStackToOneElement_ShouldKeepOnlyFirstStackElement()
        {
            // Test the LimitStackToOneElement method directly
            var testJson = @"{
  ""debugger"": {
    ""snapshot"": {
      ""stack"": [
        {
          ""function"": ""FirstFunction"",
          ""lineNumber"": 1
        },
        {
          ""function"": ""SecondFunction"",
          ""lineNumber"": 2
        },
        {
          ""function"": ""ThirdFunction"",
          ""lineNumber"": 3
        }
      ]
    }
  }
}";

            var result = NormalizeStackElement(testJson);
            var resultJson = JObject.Parse(result);
            var stackArray = resultJson.SelectToken("debugger.snapshot.stack") as JArray;

            Assert.NotNull(stackArray);
            Assert.Single(stackArray);
            Assert.Equal("TestFunction", stackArray[0]["function"].ToString());
            Assert.Equal("1", stackArray[0]["lineNumber"].ToString());
        }

        /// <summary>
        /// Validates JSON using both Newtonsoft.Json and System.Text.Json to catch malformed JSON
        /// that might be accepted by one but not the other.
        /// </summary>
        private static JObject ValidateJsonStructure(string jsonString)
        {
            // Test with Newtonsoft.Json (more lenient)
            var newtonsoftJson = JObject.Parse(jsonString);
            Assert.NotNull(newtonsoftJson);

#if NET5_0_OR_GREATER
            // Test with System.Text.Json (stricter, follows JSON spec more closely)
            // Only available in .NET 5.0 and later
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(jsonString);
                // JsonElement is a value type, so we just check if parsing succeeded
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException($"JSON is malformed according to System.Text.Json: {ex.Message}. This indicates the debugger is producing invalid JSON that may not be parseable by all JSON parsers.", ex);
            }
#endif

            return newtonsoftJson;
        }

        private static string NormalizeStackElement(string snapshot)
        {
            var json = JObject.Parse(snapshot);
            var stackToken = json.SelectToken("debugger.snapshot.stack");

            if (stackToken is JArray { Count: > 0 } stackArray)
            {
                // Keep only the first stack element
                var firstElement = stackArray[0];

                // Normalize the function name to avoid framework version differences
                if (firstElement is JObject stackElement && stackElement["function"] != null)
                {
                    stackElement["function"] = "TestFunction";
                }
                else
                {
                    throw new Exception("stack element should have function name");
                }

                stackArray.Clear();
                stackArray.Add(firstElement);
            }
            else
            {
                throw new Exception("stack should be an array");
            }

            return json.ToString(Formatting.Indented);
        }
    }
}
