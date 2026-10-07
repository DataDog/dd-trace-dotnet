// <copyright file="DuckTypeAotDiscoveryRecorderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

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
        public void MapEntriesShouldNotBeCreatedForTypesOfDynamicAssemblies()
        {
            // A type of a dynamic assembly (e.g. a proxy type dynamic duck typing generates) can't be named by a registry (the
            // generation would fail): a forward duck cast over a generated proxy is recorded with the stable type the registry
            // serves it with instead.
            var dynamicModule = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DuckTypeAotDiscoveryRecorderTests.Dynamic"), AssemblyBuilderAccess.Run)
                                               .DefineDynamicModule("DuckTypeAotDiscoveryRecorderTests.Dynamic");
            var dynamicType = dynamicModule.DefineType("DynamicType", TypeAttributes.Public).CreateTypeInfo()!.AsType();
            dynamicType.Assembly.IsDynamic.Should().BeTrue();

            DuckTypeAotDiscoveryRecorder.TryCreateMapEntry(typeof(IRecordedNameProxy), dynamicType, reverse: false, out _).Should().BeFalse();
            DuckTypeAotDiscoveryRecorder.TryCreateMapEntry(dynamicType, typeof(RecordedNameTarget), reverse: false, out _).Should().BeFalse();
            DuckTypeAotDiscoveryRecorder.TryCreateMapEntry(typeof(IRecordedNameProxy), typeof(RecordedNameTarget), reverse: false, out var mapEntry).Should().BeTrue();
            mapEntry.TargetType.Should().Be(typeof(RecordedNameTarget).FullName);
            mapEntry.TargetAssembly.Should().Be(typeof(RecordedNameTarget).Assembly.GetName().Name);
        }

        [Fact]
        public void NonPublicCoreLibraryTargetsShouldAlsoBeRecordedWithTheirClosestPublicBaseClass()
        {
            // System.RuntimeType (CoreCLR's, which other runtimes such as NativeAOT don't have) derives from System.Reflection.TypeInfo:
            // its proxy serves the runtime types the registry doesn't register.
            var runtimeType = typeof(string).GetType();
            DuckTypeAotDiscoveryRecorder.GetMapEntries(typeof(IRecordedNameProxy), runtimeType, reverse: false)
                                        .Select(entry => entry.TargetType)
                                        .Should().Equal(runtimeType.FullName, typeof(TypeInfo).FullName);

            // Public types, and reverse mappings, are recorded as they are.
            DuckTypeAotDiscoveryRecorder.GetMapEntries(typeof(IRecordedNameProxy), typeof(RecordedNameTarget), reverse: false).Should().ContainSingle();
            DuckTypeAotDiscoveryRecorder.GetMapEntries(typeof(IRecordedNameProxy), runtimeType, reverse: true).Should().ContainSingle();
        }

        [Fact]
        public void TryMergeIntoMapShouldKeepTheOtherPropertiesAndEntriesOfTheMap()
        {
            // Entries with assembly-qualified names have no assembly properties, which the recorder doesn't add (empty ones aren't
            // valid); the other properties of the map are kept.
            var outputPath = Path.Combine(_directory, "map.json");
            File.WriteAllText(outputPath, "{ \"schemaVersion\": \"1\", \"mappings\": [ { \"mode\": \"forward\", \"proxyType\": \"Ns.IQualified, ProxyAssembly\", \"targetType\": \"Ns.Qualified, TargetAssembly\" } ] }");

            DuckTypeAotDiscoveryRecorder.TryMergeIntoMap(outputPath, [RecordedEntry("Ns.IRecorded"), RecordedEntry("Ns.IAlsoRecorded")]).Should().BeTrue();

            var map = JObject.Parse(File.ReadAllText(outputPath));
            ((string?)map["schemaVersion"]).Should().Be("1");
            var mappings = ((JArray)map["mappings"]!).Cast<JObject>().ToList();
            mappings.Select(mapping => (string?)mapping["proxyType"]).Should().Equal("Ns.IQualified, ProxyAssembly", "Ns.IAlsoRecorded", "Ns.IRecorded");
            mappings[0].Properties().Select(property => property.Name).Should().Equal("mode", "proxyType", "targetType");

            // Merging the same mappings again adds nothing.
            var text = File.ReadAllText(outputPath);
            DuckTypeAotDiscoveryRecorder.TryMergeIntoMap(outputPath, [RecordedEntry("Ns.IRecorded")]).Should().BeTrue();
            File.ReadAllText(outputPath).Should().Be(text);
        }

        [Theory]
        // ASCII text in UTF-16 or UTF-32 without a byte order mark (valid UTF-8 with NUL characters), and bytes that aren't UTF-8.
        [InlineData("utf16-without-bom")]
        [InlineData("utf32-without-bom")]
        [InlineData("invalid-utf8")]
        public void TryMergeIntoMapShouldNotReplaceAMapItCantDecode(string scenario)
        {
            var outputPath = Path.Combine(_directory, "map.json");
            const string Text = "{ \"mappings\": [ { \"mode\": \"forward\", \"proxyType\": \"Ns.IExisting\", \"proxyAssembly\": \"A\", \"targetType\": \"Ns.Existing\", \"targetAssembly\": \"B\" } ] }";
            var bytes = scenario switch
            {
                "utf16-without-bom" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false).GetBytes(Text),
                "utf32-without-bom" => new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetBytes(Text),
                _ => Encoding.UTF8.GetBytes(Text).Concat(new byte[] { 0xFF, 0xFE, 0xFD }).ToArray(),
            };
            File.WriteAllBytes(outputPath, bytes);

            DuckTypeAotDiscoveryRecorder.TryMergeIntoMap(outputPath, [RecordedEntry("Ns.IRecorded")]).Should().BeFalse();
            File.ReadAllBytes(outputPath).Should().Equal(bytes);
            Directory.GetFiles(_directory).Should().ContainSingle();
        }

#if NET6_0_OR_GREATER
        [SkippableFact]
        public void TryMergeIntoMapShouldCreateTheMapADanglingSymbolicLinkPointsTo()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Creating symbolic links requires privileges on Windows.");
            var targetPath = Path.Combine(_directory, "real-map.json");
            var linkPath = Path.Combine(_directory, "map.json");
            File.CreateSymbolicLink(linkPath, targetPath);

            DuckTypeAotDiscoveryRecorder.TryMergeIntoMap(linkPath, [RecordedEntry("Ns.IRecorded")]).Should().BeTrue();

            new FileInfo(linkPath).LinkTarget.Should().Be(targetPath);
            ((string?)JObject.Parse(File.ReadAllText(targetPath))["mappings"]![0]!["proxyType"]).Should().Be("Ns.IRecorded");
        }
#endif

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

        private static DuckTypeAotDiscoveryRecorder.MapEntry RecordedEntry(string proxyType)
            => new() { Mode = "forward", ProxyType = proxyType, ProxyAssembly = "A", TargetType = "Ns.Target", TargetAssembly = "B" };

        public interface IRecordedNameProxy
        {
            string Name { get; }
        }

        public class RecordedNameTarget
        {
            public string Name => "name";
        }
    }
}
