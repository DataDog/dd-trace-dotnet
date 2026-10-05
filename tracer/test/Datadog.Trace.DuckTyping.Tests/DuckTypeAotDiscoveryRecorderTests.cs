// <copyright file="DuckTypeAotDiscoveryRecorderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckTypeAotDiscoveryRecorderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "dd-trace-discovery-recorder-tests", Guid.NewGuid().ToString("N"));

        public DuckTypeAotDiscoveryRecorderTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public void AcquireOutputLockShouldNotWaitWhenTheLockFileCannotBeCreated()
        {
            var outputPath = Path.Combine(_directory, "missing-directory", "map.json");

            var stopwatch = Stopwatch.StartNew();
            using var outputLock = DuckTypeAotDiscoveryRecorder.AcquireOutputLock(outputPath, TimeSpan.FromSeconds(30));

            outputLock.Should().BeNull();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void AcquireOutputLockShouldWaitForAnotherHolder()
        {
            var outputPath = Path.Combine(_directory, "map.json");
            using (var heldLock = DuckTypeAotDiscoveryRecorder.AcquireOutputLock(outputPath, TimeSpan.Zero))
            {
                heldLock.Should().NotBeNull();

                var stopwatch = Stopwatch.StartNew();
                using var contendedLock = DuckTypeAotDiscoveryRecorder.AcquireOutputLock(outputPath, TimeSpan.FromMilliseconds(200));

                contendedLock.Should().BeNull();
                stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(150));
            }

            using var releasedLock = DuckTypeAotDiscoveryRecorder.AcquireOutputLock(outputPath, TimeSpan.Zero);
            releasedLock.Should().NotBeNull();
        }

        [Fact]
        public void WriteAtomicallyShouldReplaceTheMapWithoutLeavingStagingFiles()
        {
            var outputPath = Path.Combine(_directory, "map.json");

            DuckTypeAotDiscoveryRecorder.WriteAtomically(outputPath, "first");
            DuckTypeAotDiscoveryRecorder.WriteAtomically(outputPath, "second");

            File.ReadAllText(outputPath).Should().Be("second");
            Directory.GetFiles(_directory).Should().ContainSingle();
        }

        [Fact]
        public void TryReadExistingMappingsShouldReadTheMapOfOtherProcesses()
        {
            var outputPath = Path.Combine(_directory, "map.json");
            File.WriteAllText(outputPath, "{\"mappings\":[{\"mode\":\"forward\",\"proxyType\":\"P\",\"proxyAssembly\":\"PA\",\"targetType\":\"T\",\"targetAssembly\":\"TA\"}]}");

            DuckTypeAotDiscoveryRecorder.TryReadExistingMappings(outputPath, out var mappings).Should().BeTrue();

            mappings.Should().ContainSingle().Which.TargetType.Should().Be("T");
        }

        [Fact]
        public void TryReadExistingMappingsShouldSetAnInvalidMapAside()
        {
            var outputPath = Path.Combine(_directory, "map.json");
            File.WriteAllText(outputPath, "{ not json");

            DuckTypeAotDiscoveryRecorder.TryReadExistingMappings(outputPath, out var mappings).Should().BeTrue();

            mappings.Should().BeEmpty();
            File.Exists(outputPath).Should().BeFalse();
            Directory.GetFiles(_directory, "map.json.*.invalid").Should().ContainSingle();
        }
    }
}
