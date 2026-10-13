// <copyright file="TestModuleCoverageMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Ci.Coverage.Metadata;

namespace Datadog.Trace.Tests.Ci;

internal class TestModuleCoverageMetadata : ModuleCoverageMetadata
{
    private static readonly FieldInfo TotalLinesField = typeof(ModuleCoverageMetadata).GetField(nameof(TotalLines))!;
    private static readonly FieldInfo CoverageModeField = typeof(ModuleCoverageMetadata).GetField(nameof(CoverageMode))!;
    private static readonly FieldInfo FilesField = typeof(ModuleCoverageMetadata).GetField("Files", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    public TestModuleCoverageMetadata()
    {
    }

    public TestModuleCoverageMetadata(int totalLines, int coverageMode, FileCoverageMetadata[] files)
        => Initialize(totalLines, coverageMode, files);

    protected void Initialize(int totalLines, int coverageMode, FileCoverageMetadata[] files)
    {
        // Production metadata is populated by the coverage rewriter. Tests use reflection to model
        // that generated state without adding a test-only construction path to the shipped assembly.
        TotalLinesField.SetValue(this, totalLines);
        CoverageModeField.SetValue(this, coverageMode);
        FilesField.SetValue(this, files);
    }
}
