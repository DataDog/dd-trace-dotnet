// <copyright file="DebuggerSnapshotCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotCollectionTests
    {
        static DebuggerSnapshotCollectionTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializingCollectionsWithNulls()
        {
            var collectionWithNulls = new List<string> { "item1", null, "item3", null };
            var snapshot = SnapshotBuilder.GenerateSnapshot(collectionWithNulls);

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
            var snapshot = SnapshotBuilder.GenerateSnapshot(emptyCollections);

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

            var snapshot = SnapshotBuilder.GenerateSnapshot(complexDict);

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
            var snapshot = SnapshotBuilder.GenerateSnapshot(concurrentCollections);

            // Verify JSON is valid
            var json = ValidateJsonStructure(snapshot);

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
