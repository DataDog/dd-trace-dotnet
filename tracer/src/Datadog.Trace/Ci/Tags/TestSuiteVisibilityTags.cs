// <copyright file="TestSuiteVisibilityTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Tags;

internal static class TestSuiteVisibilityTags
{
    /// <summary>
    /// Test session id
    /// </summary>
    public const string TestSessionId = "test_session_id";

    /// <summary>
    /// Test module id
    /// </summary>
    public const string TestModuleId = "test_module_id";

    /// <summary>
    /// Test suite id
    /// </summary>
    public const string TestSuiteId = "test_suite_id";

    /// <summary>
    /// Test session command environment variable
    /// </summary>
    public const string TestSessionCommandEnvironmentVariable = "DD_TESTSESSION_COMMAND";

    /// <summary>
    /// Test session working directory environment variable
    /// </summary>
    public const string TestSessionWorkingDirectoryEnvironmentVariable = "DD_TESTSESSION_WORKINGDIRECTORY";

    /// <summary>
    /// Test session auto injected environment variable
    /// </summary>
    public const string TestSessionAutoInjectedEnvironmentVariable = "DD_CIVISIBILITY_AUTO_INSTRUMENTATION_PROVIDER";
}
