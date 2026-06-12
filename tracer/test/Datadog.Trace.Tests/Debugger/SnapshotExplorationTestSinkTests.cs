// <copyright file="SnapshotExplorationTestSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SnapshotExplorationTestSinkTests
    {
        [Fact]
        public void Add_UsesRootLogger_WhenSnapshotContainsNestedLoggerProperty()
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                const string snapshot = """
                                        {
                                          "debugger": {
                                            "snapshot": {
                                              "captures": {
                                                "return": {
                                                  "locals": {
                                                    "local0": {
                                                      "fields": {
                                                        "logger": {
                                                          "type": "Nested.Logger",
                                                          "value": "ignored"
                                                        }
                                                      }
                                                    }
                                                  }
                                                }
                                              }
                                            }
                                          },
                                          "logger": {
                                            "name": "Top.Level.Type",
                                            "method": "DoWork"
                                          }
                                        }
                                        """;

                AddToSink(reportDirectory, "probe-id", snapshot);

                var lines = ReadReport(reportDirectory);

                lines.Should().ContainInOrder(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,Top.Level.Type,DoWork,True");
            }
            finally
            {
                Cleanup(reportDirectory);
            }
        }

        [Fact]
        public void Add_EscapesCsvSpecialCharacters()
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                const string snapshot = """
                                        {
                                          "logger": {
                                            "name": "Foo, Bar",
                                            "method": "Do\"Work"
                                          }
                                        }
                                        """;

                AddToSink(reportDirectory, "probe-id", snapshot);

                var lines = ReadReport(reportDirectory);

                lines.Should().ContainInOrder(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,\"Foo, Bar\",\"Do\"\"Work\",True");
            }
            finally
            {
                Cleanup(reportDirectory);
            }
        }

        [Fact]
        public void Add_DeduplicatesByProbeId()
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                const string snapshot = """
                                        {
                                          "logger": {
                                            "name": "Some.Type",
                                            "method": "DoWork"
                                          }
                                        }
                                        """;

                using (var sink = CreateSink(reportDirectory))
                {
                    sink.Add("probe-id", snapshot);
                    sink.Add("probe-id", snapshot);
                }

                var lines = ReadReport(reportDirectory);

                lines.Should().Equal(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,Some.Type,DoWork,True");
            }
            finally
            {
                Cleanup(reportDirectory);
            }
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("not even json")]
        [InlineData("{ \"logger\": {} }")]
        public void Add_MarksInvalid_WhenLoggerMissingOrEmpty(string snapshot)
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                AddToSink(reportDirectory, "probe-id", snapshot);

                var lines = ReadReport(reportDirectory);

                lines.Should().ContainInOrder(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,N/A,N/A,False");
            }
            finally
            {
                Cleanup(reportDirectory);
            }
        }

        [Fact]
        public void Add_MarksInvalid_WhenMethodMissing_ButStillReportsType()
        {
            var reportDirectory = CreateReportDirectory();

            try
            {
                const string snapshot = """
                                        {
                                          "logger": {
                                            "name": "Some.Type"
                                          }
                                        }
                                        """;

                AddToSink(reportDirectory, "probe-id", snapshot);

                var lines = ReadReport(reportDirectory);

                lines.Should().ContainInOrder(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,Some.Type,N/A,False");
            }
            finally
            {
                Cleanup(reportDirectory);
            }
        }

        private static string CreateReportDirectory()
        {
            var reportDirectory = Path.Combine(Path.GetTempPath(), nameof(SnapshotExplorationTestSinkTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reportDirectory);
            return reportDirectory;
        }

        private static SnapshotExplorationTestSink CreateSink(string reportDirectory)
        {
            return new SnapshotExplorationTestSink(reportDirectory, new SnapshotSlicer(DebuggerSettings.DefaultMaxDepthToSerialize, 1024 * 1024));
        }

        private static void AddToSink(string reportDirectory, string probeId, string snapshot)
        {
            using var sink = CreateSink(reportDirectory);
            sink.Add(probeId, snapshot);
        }

        private static string[] ReadReport(string reportDirectory)
        {
            var csvPath = Directory.GetFiles(reportDirectory, "*_SnapshotExplorationTestReport.csv").Single();
            return File.ReadAllLines(csvPath);
        }

        private static void Cleanup(string reportDirectory)
        {
            if (Directory.Exists(reportDirectory))
            {
                Directory.Delete(reportDirectory, recursive: true);
            }
        }
    }
}
