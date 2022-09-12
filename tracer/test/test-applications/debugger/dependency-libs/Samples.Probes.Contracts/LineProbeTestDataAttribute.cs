// <copyright file="LineProbeTestDataAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Samples.Probes.Contracts
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class LineProbeTestDataAttribute : ProbeAttributeBase
    {
        public LineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, params string[] skipOnFramework)
            : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework)
        {
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
        }

        public int LineNumber { get; }

        public int ColumnNumber { get; }
    }
}
