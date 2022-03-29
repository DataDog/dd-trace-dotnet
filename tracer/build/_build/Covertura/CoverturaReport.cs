// <copyright file="CoverturaReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Covertura
{
    public class CoverturaReport
    {
        public decimal LineRate { get; set; }
        public decimal BranchRate { get; set; }
        public int LinesCovered { get; set; }
        public int LinesValid { get; set; }
        public int BranchesCovered { get; set; }
        public int BranchesValid { get; set; }
        public int Complexity { get; set; }

        public Dictionary<string, Package> Packages { get; set; }

        public class Package
        {
            public string Name { get; set; }
            public decimal LineRate { get; set; }
            public decimal BranchRate { get; set; }
            public int Complexity { get; set; }

            public Dictionary<string, ClassDetails> Classes { get; set; }
        }
        public class ClassDetails
        {
            public string Name { get; set; }
            public string Filename { get; set; }
            public decimal LineRate { get; set; }
            public decimal BranchRate { get; set; }
            public int Complexity { get; set; }
            public int LinesCovered { get; set; }
            public int LinesValid { get; set; }
        }
    }
}
