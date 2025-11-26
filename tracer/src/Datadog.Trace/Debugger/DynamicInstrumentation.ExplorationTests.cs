// <copyright file="DynamicInstrumentation.ExplorationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;

#nullable enable

namespace Datadog.Trace.Debugger
{
    internal partial class DynamicInstrumentation
    {
        internal void WithProbesFromFile()
        {
            var probes = ReadProbesFromCsv(_settings.SnapshotExplorationTestProbesFilePath);
            UpdateAddedProbeInstrumentations(probes.Skip(700).Take(30).ToList());
        }

        private List<ProbeDefinition> ReadProbesFromCsv(string filePath)
        {
            const char parametersSeparator = '#';
            var probes = new List<ProbeDefinition>();
            using var reader = new StreamReader(filePath);

            // Skip header
            reader.ReadLine();

            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split(',');
                if (parts.Length != 5)
                {
                    Log.Warning("Invalid CSV line: {Line}", line);
                    continue;
                }

                var probe = new LogProbe
                {
                    Id = parts[0], // probeId
                    Where = new Where
                    {
                        TypeName = parts[1], // target type name (FQN)
                        MethodName = parts[2], // target method name
                        Signature = parts[3].Replace(parametersSeparator, ','), // signature
                    },
                    EvaluateAt = EvaluateAt.Exit
                };

                // ReSharper disable once UnusedVariable
                if (bool.TryParse(parts[4], out var isInstanceMethod) && probes.Count % 2 == 0)
                {
                    const string condition = """{  "ne": [    {      "ref": "this"    },    null  ]}""";

                    // Add condition for half of the instance methods
                    probe.When = new SnapshotSegment("ref this != null", condition, string.Empty);
                }

                probes.Add(probe);
            }

            return probes;
        }
    }
}
