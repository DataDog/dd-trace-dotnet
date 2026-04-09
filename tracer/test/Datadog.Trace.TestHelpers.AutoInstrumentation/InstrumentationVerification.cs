// <copyright file="InstrumentationVerification.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.TestHelpers;

public static class InstrumentationVerification
{
    /// <summary>
    /// Configuration key for enabling or disabling writing the instrumentation changes to disk
    /// (to allow for post-mortem instrumentation verification analysis).
    /// Default value is enabled.
    /// </summary>
    public const string InstrumentationVerificationEnabled = "DD_WRITE_INSTRUMENTATION_TO_DISK";

    /// <summary>
    /// Configuration key for enabling or disabling the copying original modules to disk so we can do offline investigation.
    /// Default value is disabled.
    /// </summary>
    public const string CopyingOriginalModulesEnabled = "DD_COPY_ORIGINALS_MODULES_TO_DISK";

    public static void VerifyInstrumentation(Process process, string logDirectory)
    {
        // Instrumentation verification removed — InstrumentedAssemblyGenerator/Verification projects not available
    }

    public static string FindInstrumentationLogsFolder(Process process, string logsFolder)
    {
        return null;
    }
}
