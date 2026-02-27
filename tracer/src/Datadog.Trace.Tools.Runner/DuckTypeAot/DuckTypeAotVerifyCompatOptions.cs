// <copyright file="DuckTypeAotVerifyCompatOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal sealed class DuckTypeAotVerifyCompatOptions
    {
        public DuckTypeAotVerifyCompatOptions(string compatReportPath, string compatMatrixPath, string? mappingCatalogPath)
        {
            CompatReportPath = compatReportPath;
            CompatMatrixPath = compatMatrixPath;
            MappingCatalogPath = mappingCatalogPath;
        }

        public string CompatReportPath { get; }

        public string CompatMatrixPath { get; }

        public string? MappingCatalogPath { get; }
    }
}
