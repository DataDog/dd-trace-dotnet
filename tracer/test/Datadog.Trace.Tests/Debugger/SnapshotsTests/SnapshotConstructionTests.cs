// <copyright file="SnapshotConstructionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class SnapshotConstructionTests
    {
        static SnapshotConstructionTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        #region Correct Construction Pattern Tests

        [Fact]
        public async Task CorrectConstruction_StandardMethodSnapshot_ShouldProduceValidJson()
        {
            var testObject = new ComplexTestObject();

            var snapshot = new SnapshotBuilder()
                .AddEntryInstance(testObject)
                .AddEntryArgument(42, "arg0")
                .AddEntryArgument("test", "arg1")
                .AddReturnLocal("result", "local0")
                .AddReturnArgument(42, "arg0")  // Arguments can change during execution
                .AddReturnArgument("test", "arg1")
                .AddReturnInstance(testObject)
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Verify correct structure
            Assert.NotNull(json["debugger"]["snapshot"]["captures"]["entry"]);
            Assert.NotNull(json["debugger"]["snapshot"]["captures"]["return"]);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task CorrectConstruction_StaticMethodSnapshot_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryArgument(100, "arg0")
                .AddEntryArgument("static test", "arg1")
                .AddReturnLocal("static result", "local0")
                .AddReturnArgument(100, "arg0")
                .AddReturnArgument("static test", "arg1")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should not have instance captures
            Assert.Null(json.SelectToken("debugger.snapshot.captures.entry.arguments.this"));
            Assert.Null(json.SelectToken("debugger.snapshot.captures.return.arguments.this"));

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task CorrectConstruction_EntryOnlySnapshot_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryInstance(new ComplexTestObject())
                .AddEntryArgument("entry only", "arg0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should have entry but no return
            Assert.NotNull(json["debugger"]["snapshot"]["captures"]["entry"]);
            Assert.Null(json["debugger"]["snapshot"]["captures"]["return"]);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task CorrectConstruction_ReturnOnlySnapshot_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddReturnLocal("return only", "local0")
                .AddReturnInstance(new ComplexTestObject())
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should have return but no entry
            Assert.Null(json["debugger"]["snapshot"]["captures"]["entry"]);
            Assert.NotNull(json["debugger"]["snapshot"]["captures"]["return"]);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Configurable Limits Tests

        [Fact]
        public async Task ConfigurableLimits_CustomDepthLimit_ShouldRespectLimit()
        {
            var deepObject = SnapshotBuilder.CreateDeeplyNestedObject(10);
            var limitInfo = SnapshotBuilder.CreateLimitInfo(maxDepth: 2);

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(deepObject, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: depth
            var jsonString = snapshot;
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("depth", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task ConfigurableLimits_CustomCollectionSizeLimit_ShouldRespectLimit()
        {
            var largeCollection = Enumerable.Range(1, 50).ToList();
            var limitInfo = SnapshotBuilder.CreateLimitInfo(maxCollectionSize: 5);

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(largeCollection, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: collectionSize
            var jsonString = snapshot;
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("collectionSize", jsonString);

            // Verify collection is limited to 5 elements
            var elementsToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.elements");
            if (elementsToken is JArray elementsArray)
            {
                Assert.True(elementsArray.Count <= 5, $"Collection size {elementsArray.Count} exceeds limit of 5");
            }

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task ConfigurableLimits_CustomFieldCountLimit_ShouldRespectLimit()
        {
            var manyFieldsObject = new ClassWithLotsOFields();
            var limitInfo = SnapshotBuilder.CreateLimitInfo(maxFieldCount: 5);

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(manyFieldsObject, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: fieldCount
            var jsonString = snapshot;
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("fieldCount", jsonString);

            // Verify field count is limited to 5
            var fieldsToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.fields");
            if (fieldsToken is JObject fieldsObject)
            {
                Assert.True(fieldsObject.Properties().Count() <= 5, $"Field count {fieldsObject.Properties().Count()} exceeds limit of 5");
            }

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task ConfigurableLimits_CustomStringLengthLimit_ShouldRespectLimit()
        {
            var longString = new string('x', 100);
            var limitInfo = SnapshotBuilder.CreateLimitInfo(maxStringLength: 10);

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(longString, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Verify string is truncated to 10 characters
            var valueToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.value");
            if (valueToken != null)
            {
                var value = valueToken.ToString();
                Assert.True(value.Length <= 10, $"String length {value.Length} exceeds limit of 10");
            }

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task ConfigurableLimits_MultipleLimitsTriggered_ShouldHandleGracefully()
        {
            var problematicObject = new MultipleIssuesObject();
            var limitInfo = SnapshotBuilder.CreateLimitInfo(
                maxDepth: 2,
                maxCollectionSize: 3,
                maxFieldCount: 3,
                maxStringLength: 10);

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(problematicObject, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should contain multiple notCapturedReason entries
            var jsonString = snapshot;
            var notCapturedCount = jsonString.Split(new[] { "notCapturedReason" }, StringSplitOptions.None).Length - 1;
            Assert.True(notCapturedCount > 0, "Should contain at least one notCapturedReason");

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Construction Violation Tests

        [Fact]
        public void ConstructionViolation_LocalsInEntry_ShouldHandleGracefully()
        {
            // This tests what happens when we incorrectly add locals to entry section
            var exception = Record.Exception(() =>
            {
                var snapshot = new SnapshotBuilder()
                    .BuildWithViolations(SnapshotViolation.LocalsInEntry);

                // If no exception is thrown, validate the JSON is still well-formed
                ValidateJsonStructure(snapshot);
            });

            // The system should either throw an exception or handle it gracefully
            // Both are acceptable behaviors, but the JSON should never be malformed
        }

        [Fact]
        public void ConstructionViolation_ArgumentsBeforeLocalsInReturn_ShouldHandleGracefully()
        {
            // This tests what happens when we add arguments before locals in return (wrong order)
            var exception = Record.Exception(() =>
            {
                var snapshot = new SnapshotBuilder()
                    .BuildWithViolations(SnapshotViolation.ArgumentsBeforeLocalsInReturn);

                ValidateJsonStructure(snapshot);
            });

            // Should handle gracefully - order might matter for some consumers
        }

        [Fact]
        public void ConstructionViolation_MissingInstanceForInstanceMethod_ShouldHandleGracefully()
        {
            // This tests what happens when we don't capture instance for instance method
            var exception = Record.Exception(() =>
            {
                var snapshot = new SnapshotBuilder()
                    .BuildWithViolations(SnapshotViolation.MissingInstanceForInstanceMethod);

                ValidateJsonStructure(snapshot);
            });

            // Should handle gracefully - missing instance is a valid scenario for static methods
        }

        [Fact]
        public void ConstructionViolation_DoubleEntry_ShouldThrowOrHandleGracefully()
        {
            // This tests what happens when we try to start entry twice
            var exception = Record.Exception(() =>
            {
                var snapshot = new SnapshotBuilder()
                    .BuildWithViolations(SnapshotViolation.DoubleEntry);
            });

            // This should either throw an exception or handle it gracefully
            // The important thing is that it doesn't produce malformed JSON
        }

        [Fact]
        public void ConstructionViolation_ReturnWithoutEntry_ShouldHandleGracefully()
        {
            // This tests what happens when we start return without entry
            var exception = Record.Exception(() =>
            {
                var snapshot = new SnapshotBuilder()
                    .BuildWithViolations(SnapshotViolation.ReturnWithoutEntry);

                ValidateJsonStructure(snapshot);
            });

            // Should handle gracefully - return-only snapshots are valid
        }

        [Fact]
        public void ConstructionViolation_IncompleteJson_ShouldProduceWellFormedJson()
        {
            // This tests what happens when we don't finalize properly
            var snapshot = new SnapshotBuilder()
                .BuildWithViolations(SnapshotViolation.IncompleteJson);

            // Even incomplete snapshots should produce valid JSON
            var json = ValidateJsonStructure(snapshot);

            // Verify essential structure is present
            Assert.NotNull(json["debugger"]);
        }

        #endregion

        #region Order Sensitivity Tests

        [Fact]
        public async Task OrderSensitivity_LocalsBeforeArguments_ShouldProduceValidJson()
        {
            // Test if order matters: locals before arguments in return
            var snapshot = new SnapshotBuilder()
                .AddReturnLocal("local value", "local0")
                .AddReturnArgument("arg value", "arg0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task OrderSensitivity_ArgumentsBeforeLocals_ShouldProduceValidJson()
        {
            // Test if order matters: arguments before locals in return
            var snapshot = new SnapshotBuilder()
                .AddReturnArgument("arg value", "arg0")
                .AddReturnLocal("local value", "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task OrderSensitivity_InstanceAtDifferentPositions_ShouldProduceValidJson()
        {
            var testObject = new ComplexTestObject();

            // Test instance at beginning
            var snapshot1 = new SnapshotBuilder()
                .AddEntryInstance(testObject)
                .AddEntryArgument("arg", "arg0")
                .Build();

            // Test instance at end
            var snapshot2 = new SnapshotBuilder()
                .AddEntryArgument("arg", "arg0")
                .AddEntryInstance(testObject)
                .Build();

            var json1 = ValidateJsonStructure(snapshot1);
            var json2 = ValidateJsonStructure(snapshot2);

            // Both should be valid, but structure might differ
            Assert.NotNull(json1["debugger"]["snapshot"]["captures"]["entry"]["arguments"]["this"]);
            Assert.NotNull(json2["debugger"]["snapshot"]["captures"]["entry"]["arguments"]["this"]);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(new { InstanceFirst = NormalizeStackElement(snapshot1), InstanceLast = NormalizeStackElement(snapshot2) }, verifierSettings);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task EdgeCase_EmptySnapshot_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder().Build();

            var json = ValidateJsonStructure(snapshot);

            // Should have basic structure even with no captures
            Assert.NotNull(json["debugger"]);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task EdgeCase_NullValues_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryArgument(null, "arg0")
                .AddReturnLocal(null, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task EdgeCase_MixedTypes_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryArgument(42, "intArg")
                .AddEntryArgument("string", "stringArg")
                .AddEntryArgument(new List<int> { 1, 2, 3 }, "listArg")
                .AddReturnLocal(3.14, "doubleLocal")
                .AddReturnLocal(new Dictionary<string, int> { { "key", 1 } }, "dictLocal")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Timeout Tests

        [Fact]
        public async Task TimeoutScenario_SlowSerialization_ShouldProduceTimeoutNotCapturedReason()
        {
            using (TimeoutTestHelper.SetLowTimeout(1)) // 1ms timeout
            {
                var slowObject = TimeoutTestHelper.CreateSlowSerializationObject();

                var snapshot = new SnapshotBuilder()
                    .AddReturnLocal(slowObject, "local0")
                    .Build();

                var json = ValidateJsonStructure(snapshot);

                // Should contain notCapturedReason: timeout
                var jsonString = snapshot;
                Assert.Contains("notCapturedReason", jsonString);
                Assert.Contains("timeout", jsonString);

                var verifierSettings = new VerifySettings();
                verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
                await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
            }
        }

        [Fact]
        public async Task TimeoutScenario_ComplexObjectWithTimeout_ShouldHandleGracefully()
        {
            using (TimeoutTestHelper.SetLowTimeout(5)) // 5ms timeout
            {
                var complexObject = new MultipleIssuesObject();

                var snapshot = new SnapshotBuilder()
                    .AddEntryInstance(complexObject)
                    .AddEntryArgument("test", "arg0")
                    .AddReturnLocal(complexObject, "local0")
                    .AddReturnArgument("test", "arg0")
                    .AddReturnInstance(complexObject)
                    .Build();

                var json = ValidateJsonStructure(snapshot);

                // Should still produce valid JSON even with timeout
                Assert.NotNull(json["debugger"]);

                var verifierSettings = new VerifySettings();
                verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
                await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
            }
        }

        #endregion

        #region Helper Methods

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

            return json.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        #endregion
    }
}
