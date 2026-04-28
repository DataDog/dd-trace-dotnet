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
            var reportDirectory = Path.Combine(Path.GetTempPath(), nameof(SnapshotExplorationTestSinkTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reportDirectory);

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

                using (var sink = new SnapshotExplorationTestSink(reportDirectory, new SnapshotSlicer(DebuggerSettings.DefaultMaxDepthToSerialize, 1024 * 1024)))
                {
                    sink.Add("probe-id", snapshot);
                }

                var csvPath = Directory.GetFiles(reportDirectory, "*_SnapshotExplorationTestReport.csv").Single();
                var lines = File.ReadAllLines(csvPath);

                lines.Should().ContainInOrder(
                    "Probe ID,Type,Method,Is valid",
                    "probe-id,Top.Level.Type,DoWork,True");
            }
            finally
            {
                if (Directory.Exists(reportDirectory))
                {
                    Directory.Delete(reportDirectory, recursive: true);
                }
            }
        }
    }
}
