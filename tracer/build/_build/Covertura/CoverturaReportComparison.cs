// <copyright file="CoverturaReportComparison.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Covertura
{
    public class CoverturaReportComparison
    {
        public CoverturaReport Old { get; set; }
        public CoverturaReport New { get; set; }

        public decimal LineCoverageChange { get; set; }
        public decimal BranchCoverageChange { get; set; }
        public int ComplexityChange { get; set; }

        public List<PackageChanges> MatchedPackages { get; set; } = new();
        public List<CoverturaReport.Package> NewPackages { get; set; } = new();
        public List<CoverturaReport.Package> RemovedPackages { get; set; } = new();

        public class PackageChanges
        {
            public CoverturaReport.Package Old { get; set; }
            public CoverturaReport.Package New { get; set; }

            public decimal LineCoverageChange { get; set; }
            public decimal BranchCoverageChange { get; set; }
            public int ComplexityChange { get; set; }

            public Dictionary<string, ClassChanges> ClassChanges { get;  } = new();
            public List<CoverturaReport.ClassDetails> RemovedClasses { get;  } = new();
            public List<CoverturaReport.ClassDetails> NewClasses { get; set; } = new();
        }

        public class ClassChanges
        {
            public string Name { get; set; }
            public string Filename { get; set; }
            public decimal LineCoverageChange { get; set; }
            public decimal BranchCoverageChange { get; set; }
            public int ComplexityChange { get; set; }
            public bool IsSignificantChange { get; set; }
        }
    }
}
