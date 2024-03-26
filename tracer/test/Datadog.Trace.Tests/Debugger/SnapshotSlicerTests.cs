// <copyright file="SnapshotSlicerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Snapshots;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class SnapshotSlicerTests
    {
        [Fact]
        public void SnapshotSmallerThanMaxSize_NothingSliced()
        {
            var snapshot = SnapshotHelper.GenerateSnapshot(new SimpleClass(), false);
            var slicer = GetSlicer(3, snapshot.Length + 1);
            var modifiedSnapshot = slicer.SliceIfNeeded("id", snapshot);
            Assert.Equal(snapshot, modifiedSnapshot);
        }

        [Fact]
        public async Task SnapshotBiggerThanMaxSize_OneLevel_LevelSliced()
        {
            var snapshot = SnapshotHelper.GenerateSnapshot(new ComplexClass(), false);
            var slicer = GetSlicer(3, snapshot.Length - 1);
            var modifiedSnapshot = slicer.SliceIfNeeded("id", snapshot);

            var captures = JObject.Parse(modifiedSnapshot).SelectToken("debugger.snapshot.captures");
            await Verifier.Verify(captures);
        }

        [Fact]
        public async Task SnapshotBiggerThanMaxSize_TwoLevel_OneSliced()
        {
            var snapshot = SnapshotHelper.GenerateSnapshot(new VeryComplexClass() { ComplexClass = new ComplexClass() { SimpleClass = new SimpleClass() } }, false);
            var slicer = GetSlicer(3, snapshot.Length - 1);
            var modifiedSnapshot = slicer.SliceIfNeeded("id", snapshot);

            var captures = JObject.Parse(modifiedSnapshot).SelectToken("debugger.snapshot.captures");
            await Verifier.Verify(captures);
        }

        [Fact]
        public async Task SnapshotBiggerThanMaxSize_ThreeLevel_AllSliced()
        {
            var snapshot = SnapshotHelper.GenerateSnapshot(new VeryComplexClass() { Class = new VeryComplexClass() { ComplexClass = new ComplexClass() { SimpleClass = new SimpleClass() } } }, false);
            var slicer = GetSlicer(3, snapshot.Length - 326);
            var modifiedSnapshot = slicer.SliceIfNeeded("id", snapshot);

            var captures = JObject.Parse(modifiedSnapshot).SelectToken("debugger.snapshot.captures");
            await Verifier.Verify(captures);
        }

        private SnapshotSlicer GetSlicer(int maxDepth, int maxSize)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.MaxDepthToSerialize, maxDepth.ToString() }, }),
                NullConfigurationTelemetry.Instance);

            return SnapshotSlicer.Create(settings, maxSize);
        }

        private class SimpleClass
        {
        }

        private class ComplexClass
        {
            public SimpleClass SimpleClass { get; set; }
        }

        private class VeryComplexClass
        {
            public VeryComplexClass Class { get; set; }

            public ComplexClass ComplexClass { get; set; }
        }
    }
}
