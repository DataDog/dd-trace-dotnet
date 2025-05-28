// <copyright file="DebuggerSnapshotLimitEnforcementTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotLimitEnforcementTests : DebuggerSnapshotCreatorTests
    {
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
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

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }
    }
}
