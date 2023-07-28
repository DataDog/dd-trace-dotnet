// <copyright file="TestTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Span tags for test data model
/// </summary>
internal static class TestTags
{
    /// <summary>
    /// Test bundle name
    /// </summary>
    public const string Bundle = "test.bundle";

    /// <summary>
    /// Test module name
    /// </summary>
    public const string Module = "test.module";

    /// <summary>
    /// Test suite name
    /// </summary>
    public const string Suite = "test.suite";

    /// <summary>
    /// Test name
    /// </summary>
    public const string Name = "test.name";

    /// <summary>
    /// Test type
    /// </summary>
    public const string Type = "test.type";

    /// <summary>
    /// Test type test
    /// </summary>
    public const string TypeTest = "test";

    /// <summary>
    /// Test type benchmark
    /// </summary>
    public const string TypeBenchmark = "benchmark";

    /// <summary>
    /// Test framework
    /// </summary>
    public const string Framework = "test.framework";

    /// <summary>
    /// Test framework version
    /// </summary>
    public const string FrameworkVersion = "test.framework_version";

    /// <summary>
    /// Test parameters
    /// </summary>
    public const string Parameters = "test.parameters";

    /// <summary>
    /// Test traits
    /// </summary>
    public const string Traits = "test.traits";

    /// <summary>
    /// Test status
    /// </summary>
    public const string Status = "test.status";

    /// <summary>
    /// Test Pass status
    /// </summary>
    public const string StatusPass = "pass";

    /// <summary>
    /// Test Fail status
    /// </summary>
    public const string StatusFail = "fail";

    /// <summary>
    /// Test Skip status
    /// </summary>
    public const string StatusSkip = "skip";

    /// <summary>
    /// Test skip reason
    /// </summary>
    public const string SkipReason = "test.skip_reason";

    /// <summary>
    /// Test output message
    /// </summary>
    public const string Message = "test.message";

    /// <summary>
    /// Parameters metadata TestName
    /// </summary>
    public const string MetadataTestName = "test_name";

    /// <summary>
    /// Origin value for CIApp Test
    /// </summary>
    public const string CIAppTestOriginName = "ciapp-test";

    /// <summary>
    /// Test source file
    /// </summary>
    public const string SourceFile = "test.source.file";

    /// <summary>
    /// Test source start line
    /// </summary>
    public const string SourceStart = "test.source.start";

    /// <summary>
    /// Test source end line
    /// </summary>
    public const string SourceEnd = "test.source.end";

    /// <summary>
    /// Test code owners
    /// </summary>
    public const string CodeOwners = "test.codeowners";

    /// <summary>
    /// Test command
    /// </summary>
    public const string Command = "test.command";

    /// <summary>
    /// Test command exit code
    /// </summary>
    public const string CommandExitCode = "test.exit_code";

    /// <summary>
    /// Test command working directory
    /// </summary>
    public const string CommandWorkingDirectory = "test.working_directory";
}
