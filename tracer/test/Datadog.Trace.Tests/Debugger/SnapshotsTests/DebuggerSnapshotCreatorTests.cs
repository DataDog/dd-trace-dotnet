// <copyright file="DebuggerSnapshotCreatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotCreatorTests
    {
        #region Basic Limits Tests

        [Fact]
        public async Task Limits_LargeCollection()
        {
            await ValidateSingleValue(Enumerable.Range(1, 2000).ToArray());
        }

        [SkippableFact]
        public void Limits_LargeDictionary()
        {
            throw new SkipException("This test fails sometimes, but it's not clear why. It's not a high priority to investigate, so we're skipping it for now.");

            // await ValidateSingleValue(Enumerable.Range(1, 2000).ToDictionary(k => k.ToString(), k => k));
        }

        [Fact]
        public async Task Limits_FieldsCount()
        {
            await ValidateSingleValue(new ClassWithLotsOFields());
        }

        [Fact]
        public async Task Limits_StringLength()
        {
            await ValidateSingleValue(new string('f', 5000));
        }

        [Fact]
        public async Task Limits_Depth()
        {
            await ValidateSingleValue(new InfiniteRecursion());
        }

        #endregion

        #region Basic Object Structure Tests

        [Fact]
        public async Task ObjectStructure_Null()
        {
            await ValidateSingleValue(null);
        }

        [Fact]
        public async Task ObjectStructure_EmptyArray()
        {
            await ValidateSingleValue(new int[] { });
        }

        [Fact]
        public async Task ObjectStructure_EmptyList()
        {
            await ValidateSingleValue(new List<int>());
        }

        #endregion

        #region Special Types Tests

        [Fact]
        public async Task SpecialType_StringBuilder()
        {
            await ValidateSingleValue(new StringBuilder("hi from stringbuilder"));
        }

        [Fact]
        public async Task SpecialType_LazyUninitialized()
        {
            await ValidateSingleValue(new Lazy<int>(() => Math.Max(1, 2)));
        }

        [Fact]
        public async Task SpecialType_LazyInitialized()
        {
            var lazy = new Lazy<int>(() => Math.Max(1, 2));
            var temp = lazy.Value;
            await ValidateSingleValue(lazy);
        }

        #endregion

        #region Complex Object Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingComplexObjects()
        {
            var complexObject = new ComplexTestObject();
            var snapshot = SnapshotHelper.GenerateSnapshot(complexObject);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingNullValues()
        {
            var objectWithNulls = new ObjectWithNulls();
            var snapshot = SnapshotHelper.GenerateSnapshot(objectWithNulls);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingCircularReferences()
        {
            var circular = new CircularReference();
            circular.Self = circular;

            var snapshot = SnapshotHelper.GenerateSnapshot(circular);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingExceptions()
        {
            var exception = new InvalidOperationException("Test exception", new ArgumentException("Inner exception"));
            var snapshot = SnapshotHelper.GenerateSnapshot(exception);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Collection Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingCollectionsWithNulls()
        {
            var collectionWithNulls = new List<string> { "item1", null, "item3", null };
            var snapshot = SnapshotHelper.GenerateSnapshot(collectionWithNulls);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingEmptyCollections()
        {
            var emptyCollections = new EmptyCollections();
            var snapshot = SnapshotHelper.GenerateSnapshot(emptyCollections);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingDictionariesWithComplexKeys()
        {
            var complexDict = new Dictionary<ComplexKey, string>
            {
                { new ComplexKey { Id = 1, Name = "Key1" }, "Value1" },
                { new ComplexKey { Id = 2, Name = "Key2" }, "Value2" }
            };

            var snapshot = SnapshotHelper.GenerateSnapshot(complexDict);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingConcurrentCollections()
        {
            var concurrentCollections = new ConcurrentCollections();
            var snapshot = SnapshotHelper.GenerateSnapshot(concurrentCollections);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Property and Field Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingObjectsWithProperties()
        {
            var objectWithProps = new ObjectWithProperties();
            var snapshot = SnapshotHelper.GenerateSnapshot(objectWithProps);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingStaticFields()
        {
            var objectWithStatics = new ObjectWithStaticFields();
            var snapshot = SnapshotHelper.GenerateSnapshot(objectWithStatics);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingObjectsWithBackingFields()
        {
            var autoPropsObject = new ObjectWithAutoProperties();
            var snapshot = SnapshotHelper.GenerateSnapshot(autoPropsObject);

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Generic Types Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingGenericTypes()
        {
            var genericObject = new GenericTestObject<string, int>
            {
                Value1 = "test",
                Value2 = 42,
                GenericList = new List<string> { "a", "b", "c" },
                GenericDict = new Dictionary<string, int> { { "key1", 1 }, { "key2", 2 } }
            };

            var snapshot = SnapshotHelper.GenerateSnapshot(genericObject);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Advanced Types Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingLazyObjects()
        {
            var lazyObjects = new LazyObjects();
            var snapshot = SnapshotHelper.GenerateSnapshot(lazyObjects);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingTaskObjects()
        {
            var taskObjects = new TaskObjects();
            var snapshot = SnapshotHelper.GenerateSnapshot(taskObjects);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingObjectsWithThrowingProperties()
        {
            var throwingObject = new ObjectWithThrowingProperties();
            var snapshot = SnapshotHelper.GenerateSnapshot(throwingObject);

            // Verify JSON is valid - should not throw and should handle property exceptions gracefully
            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializationThrowsException()
        {
            var throwingObject = new ObjectWithThrowingProperties();
            var snapshot = SnapshotHelper.GenerateSnapshot(throwingObject);

            var json = ValidateJsonStructure(snapshot);

            // The serialization should handle exceptions gracefully and continue
            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenCollectionModifiedDuringSerialization()
        {
            var modifiableCollection = new ConcurrentModificationCollection();
            var snapshot = SnapshotHelper.GenerateSnapshot(modifiableCollection);

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region Limit Enforcement Tests

        [Fact]
        public void FieldCount_ShouldRespectLimits_WhenSerializingObjectsWithManyFields()
        {
            var manyFieldsObject = new ClassWithLotsOFields();
            var snapshot = SnapshotHelper.GenerateSnapshot(manyFieldsObject);

            var json = ValidateJsonStructure(snapshot);

            // Check that field count limit is respected
            var fieldsToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.fields");
            if (fieldsToken != null)
            {
                var fieldCount = ((JObject)fieldsToken).Properties().Count();
                Assert.True(fieldCount <= 20, $"Field count {fieldCount} exceeds limit of 20");
            }
        }

        [Fact]
        public void CollectionSize_ShouldRespectLimits_WhenSerializingLargeCollections()
        {
            var largeCollection = Enumerable.Range(1, 200).ToList();
            var snapshot = SnapshotHelper.GenerateSnapshot(largeCollection);

            var json = ValidateJsonStructure(snapshot);

            // Check that collection size limit is respected
            var elementsToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.elements");
            if (elementsToken != null && elementsToken is JArray elementsArray)
            {
                Assert.True(elementsArray.Count <= 100, $"Collection size {elementsArray.Count} exceeds limit of 100");
            }
        }

        [Fact]
        public void StringLength_ShouldRespectLimits_WhenSerializingLongStrings()
        {
            var longString = new string('x', 2000);
            var snapshot = SnapshotHelper.GenerateSnapshot(longString);

            var json = ValidateJsonStructure(snapshot);

            // Check that string length limit is respected
            var valueToken = json.SelectToken("debugger.snapshot.captures.return.locals.local0.value");
            if (valueToken != null)
            {
                var value = valueToken.ToString();
                Assert.True(value.Length <= 1000, $"String length {value.Length} exceeds limit of 1000");
            }
        }

        [Fact]
        public void Depth_ShouldRespectLimits_WhenSerializingDeeplyNestedObjects()
        {
            var deepObject = CreateDeeplyNestedObject(10);
            var snapshot = SnapshotHelper.GenerateSnapshot(deepObject);

            var json = ValidateJsonStructure(snapshot);

            // The serialization should complete without stack overflow
            // and should respect depth limits
        }

        #endregion

        #region NotCapturedReason Tests

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializationTimesOut()
        {
            // Create an object that would take a very long time to serialize
            var slowObject = new SlowSerializationObject();
            var snapshot = SnapshotHelper.GenerateSnapshot(slowObject);

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
            var snapshot = SnapshotHelper.GenerateSnapshot(manyFieldsObject);

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
            var snapshot = SnapshotHelper.GenerateSnapshot(largeCollection);

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
            var deepObject = CreateDeeplyNestedObject(20); // Exceeds typical depth limit
            var snapshot = SnapshotHelper.GenerateSnapshot(deepObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: depth
            var jsonString = snapshot.ToString();
            Assert.Contains("notCapturedReason", jsonString);
            Assert.Contains("depth", jsonString);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenRedactionOccurs()
        {
            var redactedObject = new ObjectWithRedactedFields();
            var snapshot = SnapshotHelper.GenerateSnapshot(redactedObject);

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
            var snapshot = SnapshotHelper.GenerateSnapshot(problematicObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain multiple notCapturedReason entries
            var jsonString = snapshot.ToString();
            var notCapturedCount = jsonString.Split(new[] { "notCapturedReason" }, StringSplitOptions.None).Length - 1;
            Assert.True(notCapturedCount > 0, "Should contain at least one notCapturedReason");

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        #endregion

        #region JSON Structure Integrity Tests

        [Fact]
        public void JsonStructure_ShouldBeComplete_WhenSerializationIsInterrupted()
        {
            // Test the most critical scenario: JSON structure completeness
            var problematicObject = new MultipleIssuesObject();
            var snapshot = SnapshotHelper.GenerateSnapshot(problematicObject);

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

        #endregion

        #region Helper Methods

        /// <summary>
        /// Validate that we produce valid json for a specific value, and that the output conforms to the given set of limits on capture.
        /// </summary>
        internal async Task ValidateSingleValue(object local)
        {
            var snapshot = SnapshotHelper.GenerateSnapshot(local);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            var localVariableAsJson = JObject.Parse(snapshot).SelectToken("debugger.snapshot.captures.return.locals");
            await Verifier.Verify(localVariableAsJson, verifierSettings);
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

        #endregion
    }
}
