// <copyright file="DebuggerSnapshotLimitEnforcementTests.cs" company="Datadog">
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
    public class DebuggerSnapshotLimitEnforcementTests
    {
        static DebuggerSnapshotLimitEnforcementTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        [Fact]
        public void FieldCount_ShouldRespectLimits_WhenSerializingObjectsWithManyFields()
        {
            var manyFieldsObject = new ClassWithLotsOFields();
            var snapshot = SnapshotBuilder.GenerateSnapshot(manyFieldsObject);

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
            var snapshot = SnapshotBuilder.GenerateSnapshot(largeCollection);

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
            var snapshot = SnapshotBuilder.GenerateSnapshot(longString);

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
            var deepObject = SnapshotBuilder.CreateDeeplyNestedObject(10);
            var snapshot = SnapshotBuilder.GenerateSnapshot(deepObject);

            var json = ValidateJsonStructure(snapshot);

            // Should contain notCapturedReason: depth
            Assert.Contains("notCapturedReason", snapshot);
            Assert.Contains("depth", snapshot);
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
    }
}
