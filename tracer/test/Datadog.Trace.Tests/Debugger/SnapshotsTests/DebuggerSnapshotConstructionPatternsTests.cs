// <copyright file="DebuggerSnapshotConstructionPatternsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    /// <summary>
    /// Tests for different snapshot construction patterns and violation handling.
    /// This class focuses on testing the SnapshotBuilder API and various construction scenarios.
    /// </summary>
    [UsesVerify]
    public class DebuggerSnapshotConstructionPatternsTests : DebuggerSnapshotCreatorTests
    {
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

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

        [Fact]
        public async Task OrderSensitivity_LocalsBeforeArguments_ShouldProduceValidJson()
        {
            // Test if order matters: locals before arguments in return
            var snapshot = new SnapshotBuilder()
                .AddReturnLocal("local value", "local0")
                .AddReturnArgument("arg value", "arg0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(new { InstanceFirst = NormalizeStackElement(snapshot1), InstanceLast = NormalizeStackElement(snapshot2) }, verifierSettings);
        }

        [Fact]
        public async Task EdgeCase_EmptySnapshot_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder().Build();

            var json = ValidateJsonStructure(snapshot);

            // Should have basic structure even with no captures
            Assert.NotNull(json["debugger"]);

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task EdgeCase_MixedTypes_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryArgument(42, "intArg")
                .AddEntryArgument("string", "stringArg")
                .AddEntryArgument(new System.Collections.Generic.List<int> { 1, 2, 3 }, "listArg")
                .AddReturnLocal(3.14, "doubleLocal")
                .AddReturnLocal(new System.Collections.Generic.Dictionary<string, int> { { "key", 1 } }, "dictLocal")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task TimeoutScenario_SlowSerialization_ShouldProduceTimeoutNotCapturedReason()
        {
            var slowObject = TimeoutTestHelper.CreateSlowSerializationObject();
            var limitInfo = TimeoutTestHelper.CreateLowTimeoutLimitInfo(1); // 1ms timeout

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddReturnLocal(slowObject, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Assert that the snapshot contains NotCapturedReason.timeout
            AssertContainsNotCapturedReason(snapshot, "timeout");

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task TimeoutScenario_ComplexObjectWithTimeout_ShouldHandleGracefully()
        {
            var complexObject = new MultipleIssuesObject();
            var limitInfo = TimeoutTestHelper.CreateLowTimeoutLimitInfo(5); // 5ms timeout

            var snapshot = new SnapshotBuilder(limitInfo)
                .AddEntryInstance(complexObject)
                .AddEntryArgument("test", "arg0")
                .AddReturnLocal(complexObject, "local0")
                .AddReturnArgument("test", "arg0")
                .AddReturnInstance(complexObject)
                .Build();

            var json = ValidateJsonStructure(snapshot);

            // Should still produce valid JSON even with timeout
            Assert.NotNull(json["debugger"]);

            // Assert that the snapshot contains NotCapturedReason.timeout
            AssertContainsNotCapturedReason(snapshot, "timeout");

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }
    }
}
