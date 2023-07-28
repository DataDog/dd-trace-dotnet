// <copyright file="TestModuleSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tagging;

internal partial class TestModuleSpanTags : TestSessionSpanTags
{
    private int _itrSkippingCount;

    public TestModuleSpanTags()
    {
    }

    public TestModuleSpanTags(TestSessionSpanTags sessionTags)
    {
        SessionId = sessionTags.SessionId;
        Command = sessionTags.Command;
        WorkingDirectory = sessionTags.WorkingDirectory;
    }

    public ulong ModuleId { get; set; }

    [Tag(TestTags.Type)]
    public string Type { get; set; }

    [Tag(TestTags.Module)]
    public string Module { get; set; }

    [Tag(TestTags.Bundle)]
    public string Bundle => Module;

    [Tag(TestTags.Framework)]
    public string Framework { get; set; }

    [Tag(TestTags.FrameworkVersion)]
    public string FrameworkVersion { get; set; }

    [Tag(CommonTags.RuntimeName)]
    public string RuntimeName { get; set; }

    [Tag(CommonTags.RuntimeVersion)]
    public string RuntimeVersion { get; set; }

    [Tag(CommonTags.RuntimeArchitecture)]
    public string RuntimeArchitecture { get; set; }

    [Tag(CommonTags.OSArchitecture)]
    public string OSArchitecture { get; set; }

    [Tag(CommonTags.OSPlatform)]
    public string OSPlatform { get; set; }

    [Tag(CommonTags.OSVersion)]
    public string OSVersion { get; set; }

    [Metric(IntelligentTestRunnerTags.SkippingCount)]
    public double? IntelligentTestRunnerSkippingCount => _itrSkippingCount == 0 ? null : _itrSkippingCount;

    internal void AddIntelligentTestRunnerSkippingCount(int increment)
    {
        Interlocked.Add(ref _itrSkippingCount, increment);
    }
}
