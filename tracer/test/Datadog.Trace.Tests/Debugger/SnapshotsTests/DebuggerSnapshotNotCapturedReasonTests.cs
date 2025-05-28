// <copyright file="DebuggerSnapshotNotCapturedReasonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotNotCapturedReasonTests : DebuggerSnapshotCreatorTests
    {
        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenSerializationTimesOut()
        {
            // Create an object that would take a very long time to serialize
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
        public async Task JsonStructure_ShouldBeValid_WhenFieldCountLimitExceeded()
        {
            var manyFieldsObject = new ClassWithLotsOFields();
            var snapshot = SnapshotBuilder.GenerateSnapshot(manyFieldsObject);

            var json = ValidateJsonStructure(snapshot);

            // Assert that the snapshot contains NotCapturedReason.fieldCount
            AssertContainsNotCapturedReason(snapshot, "fieldCount");

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenCollectionSizeLimitExceeded()
        {
            var largeCollection = Enumerable.Range(1, 500).ToList();
            var snapshot = SnapshotBuilder.GenerateSnapshot(largeCollection);

            var json = ValidateJsonStructure(snapshot);

            // Assert that the snapshot contains NotCapturedReason.collectionSize
            AssertContainsNotCapturedReason(snapshot, "collectionSize");

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }

        [Fact]
        public async Task JsonStructure_ShouldBeValid_WhenDepthLimitExceeded()
        {
            var deepObject = SnapshotBuilder.CreateDeeplyNestedObject(20); // Exceeds typical depth limit
            var snapshot = SnapshotBuilder.GenerateSnapshot(deepObject);

            var json = ValidateJsonStructure(snapshot);

            // Assert that the snapshot contains NotCapturedReason.depth
            AssertContainsNotCapturedReason(snapshot, "depth");

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }
    }
}
