// <copyright file="DebuggerSnapshotNotCapturedReasonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotNotCapturedReasonTests
    {
        static DebuggerSnapshotNotCapturedReasonTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializationTimesOut()
        {
            // Create an object that would take a very long time to serialize
            var slowObject = new SlowSerializationObject();
            var snapshot = SnapshotBuilder.GenerateSnapshot(slowObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: timeout somewhere in the structure
            var jsonString = snapshot.ToString();
            Assert.Contains("notCapturedReason", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenFieldCountLimitExceeded()
        {
            var manyFieldsObject = new ClassWithLotsOFields();
            var snapshot = SnapshotBuilder.GenerateSnapshot(manyFieldsObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: fieldCount
            var jsonString = snapshot.ToString();
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("fieldCount", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenCollectionSizeLimitExceeded()
        {
            var largeCollection = Enumerable.Range(1, 500).ToList();
            var snapshot = SnapshotBuilder.GenerateSnapshot(largeCollection);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: collectionSize
            var jsonString = snapshot.ToString();
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("collectionSize", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenDepthLimitExceeded()
        {
            var deepObject = SnapshotBuilder.CreateDeeplyNestedObject(20); // Exceeds typical depth limit
            var snapshot = SnapshotBuilder.GenerateSnapshot(deepObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: depth
            Assert.Contains("notCapturedReason", snapshot);
            Assert.Contains("depth", snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenRedactionOccurs()
        {
            var redactedObject = new ObjectWithRedactedFields();
            var snapshot = SnapshotBuilder.GenerateSnapshot(redactedObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: redactedIdent or redactedType
            var jsonString = snapshot.ToString();
            Assert.Contains("notCapturedReason", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenMultipleNotCapturedReasonsOccur()
        {
            // Create an object that triggers multiple NotCapturedReason conditions
            var problematicObject = new MultipleIssuesObject();
            var snapshot = SnapshotBuilder.GenerateSnapshot(problematicObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain multiple notCapturedReason entries
            var jsonString = snapshot.ToString();
            var notCapturedCount = jsonString.Split(new[] { "notCapturedReason" }, StringSplitOptions.None).Length - 1;
            Assert.True(notCapturedCount > 0, "Should contain at least one notCapturedReason");

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
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
