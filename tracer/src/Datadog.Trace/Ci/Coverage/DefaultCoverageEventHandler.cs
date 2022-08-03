// <copyright file="DefaultCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Coverage.Models;

namespace Datadog.Trace.Ci.Coverage
{
    internal sealed class DefaultCoverageEventHandler : CoverageEventHandler
    {
        protected override object OnSessionFinished(CoverageInstruction[] coverageInstructions)
        {
            if (coverageInstructions == null || coverageInstructions.Length == 0)
            {
                return null;
            }

            var groupByFiles = coverageInstructions.GroupBy(i => i.FilePath).ToList();
            var coveragePayload = new CoveragePayload();

            foreach (var boundariesPerFile in groupByFiles)
            {
                var fileName = boundariesPerFile.Key;
                var coverageFileName = new FileCoverage
                {
                    FileName = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(fileName, false)
                };

                coveragePayload.Files.Add(coverageFileName);

                foreach (var rangeGroup in boundariesPerFile.GroupBy(i => i.Range))
                {
                    var range = rangeGroup.Key;
                    var endColumn = (ushort)(range & 0xFFFFFF);
                    var endLine = (ushort)((range >> 16) & 0xFFFFFF);
                    var startColumn = (ushort)((range >> 32) & 0xFFFFFF);
                    var startLine = (ushort)((range >> 48) & 0xFFFFFF);
                    var num = rangeGroup.Count();
                    coverageFileName.Segments.Add(new[] { startLine, startColumn, endLine, endColumn, (uint)num });
                }
            }

            return coveragePayload;
        }
    }
}
