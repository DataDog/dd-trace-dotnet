// <copyright file="DynamicInstrumentation.ExplorationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Debugger.Configurations.Models;

#nullable enable

namespace Datadog.Trace.Debugger
{
    internal partial class DynamicInstrumentation
    {
        // -------------------------
        // Helper: deterministic selection
        // -------------------------

        /// <summary>
        /// Normalize a string for the canonical key: trim, lowercase (invariant), collapse whitespace, remove surrounding quotes.
        /// </summary>
        private static string NormalizeForKey(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\s+", " ");
            if (s.Length >= 2 &&
                ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'"))))
            {
                s = s.Substring(1, s.Length - 2);
            }

            return s;
        }

        /// <summary>
        /// Compute SHA-1 and return the first 8 bytes as an unsigned 64-bit integer (BitConverter.ToUInt64).
        /// This produces a stable integer suitable for modulo selection.
        /// </summary>
        private static ulong Sha1ToUInt64(string s)
        {
            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(s);
            var hash = sha1.ComputeHash(bytes); // 20 bytes
            return BitConverter.ToUInt64(hash, 0); // take first 8 bytes
        }

        /// <summary>
        /// Decide whether to select a probe given type, method and signature.
        /// Default thresholdPercent = 50 selects ~50% deterministically.
        /// </summary>
        private static bool ShouldSelectProbeBySignature(string type, string method, string signature, int thresholdPercent = 50)
        {
            if (thresholdPercent <= 0)
            {
                return false;
            }

            if (thresholdPercent >= 100)
            {
                return true;
            }

            var key = $"{NormalizeForKey(type)}|{NormalizeForKey(method)}|{NormalizeForKey(signature)}";

            try
            {
                var v = Sha1ToUInt64(key);
                return (v % 100UL) < (ulong)thresholdPercent;
            }
            catch
            {
                // On any unexpected error, choose conservative behavior: do not select the probe.
                return false;
            }
        }

        internal int WithProbesFromFile()
        {
            var probes = ReadProbesFromCsv(_settings.SnapshotExplorationTestProbesFilePath);
            UpdateAddedProbeInstrumentations(probes);
            return probes.Count;
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

                var signature = parts[3].Replace(parametersSeparator, ','); // signature

                var probe = new LogProbe
                {
                    Id = parts[0], // probeId
                    Where = new Where
                    {
                        TypeName = parts[1], // target type name (FQN)
                        MethodName = parts[2], // target method name
                        Signature = signature,
                    },
                    EvaluateAt = EvaluateAt.Exit,
                    CaptureSnapshot = true
                };

                // Add condition for a deterministic subset of instance methods
                if (bool.TryParse(
                        parts[4],
                        out var isInstanceMethod)
                 && isInstanceMethod
                 && ShouldSelectProbeBySignature(probe.Where.TypeName, probe.Where.MethodName, probe.Where.Signature, 50))
                {
                    const string condition = """{  "ne": [    {      "ref": "this"    },    null  ]}""";
                    probe.When = new SnapshotSegment("ref this != null", condition, string.Empty);
                }

                probes.Add(probe);
            }

            return probes;
        }
    }
}
