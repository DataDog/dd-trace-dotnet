// <copyright file="DebuggerSnapshotCreatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Base class for all debugger snapshot tests with shared helper methods.
    /// Individual test methods have been moved to specialized test classes:
    /// - DebuggerSnapshotLimitsTests: Basic limits tests
    /// - DebuggerSnapshotObjectStructureTests: Basic object structure tests
    /// - DebuggerSnapshotSpecialTypesTests: Special types tests
    /// - DebuggerSnapshotComplexObjectTests: Complex object tests
    /// - DebuggerSnapshotCollectionTests: Collection tests
    /// - DebuggerSnapshotPropertyFieldTests: Property and field tests
    /// - DebuggerSnapshotGenericTypesTests: Generic types tests
    /// - DebuggerSnapshotAdvancedTypesTests: Advanced types tests
    /// - DebuggerSnapshotErrorHandlingTests: Error handling tests
    /// - DebuggerSnapshotLimitEnforcementTests: Limit enforcement tests
    /// - DebuggerSnapshotNotCapturedReasonTests: NotCapturedReason tests
    /// - DebuggerSnapshotJsonStructureTests: JSON structure integrity tests
    /// - SnapshotConstructionTests: Snapshot construction patterns and violations
    /// </summary>
    [UsesVerify]
    public abstract class DebuggerSnapshotCreatorTests
    {
        static DebuggerSnapshotCreatorTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        /// <summary>
        /// Validate that we produce valid json for a specific value, and that the output conforms to the given set of limits on capture.
        /// </summary>
        protected static async Task ValidateSingleValue(object local)
        {
            var snapshot = SnapshotBuilder.GenerateSnapshot(local);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            var localVariableAsJson = JObject.Parse(snapshot).SelectToken("debugger.snapshot.captures.return.locals");
            await Verifier.Verify(localVariableAsJson, verifierSettings);
        }

        /// <summary>
        /// Validates JSON using both Newtonsoft.Json and System.Text.Json to catch malformed JSON
        /// that might be accepted by one but not the other.
        /// </summary>
        protected static JObject ValidateJsonStructure(string jsonString)
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

        /// <summary>
        /// Normalizes stack elements in snapshots for consistent testing by keeping only the first stack element
        /// and normalizing the function name to avoid framework version differences.
        /// </summary>
        protected static string NormalizeStackElement(string snapshot)
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

        /// <summary>
        /// Creates standard verifier settings with common scrubbing rules for snapshot tests.
        /// </summary>
        protected static VerifySettings CreateStandardVerifierSettings()
        {
            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            return verifierSettings;
        }

        /// <summary>
        /// Asserts that the snapshot contains at least one occurrence of the specified NotCapturedReason.
        /// This method searches through the entire JSON structure to find any notCapturedReason field
        /// with the expected value.
        /// </summary>
        /// <param name="snapshot">The snapshot JSON string to search</param>
        /// <param name="expectedReason">The expected NotCapturedReason value (e.g., "timeout", "depth", "fieldCount")</param>
        protected static void AssertContainsNotCapturedReason(string snapshot, string expectedReason)
        {
            var json = JObject.Parse(snapshot);

            // Search for all notCapturedReason fields in the JSON
            var notCapturedReasonTokens = json.SelectTokens("$..notCapturedReason");

            var foundReasons = notCapturedReasonTokens
                .Select(token => token.Value<string>())
                .Where(reason => !string.IsNullOrEmpty(reason))
                .ToList();

            Assert.True(
                foundReasons.Any(reason => reason.Equals(expectedReason, StringComparison.OrdinalIgnoreCase)),
                $"Expected to find NotCapturedReason '{expectedReason}' in snapshot, but found: [{string.Join(", ", foundReasons)}]");
        }
    }
}
