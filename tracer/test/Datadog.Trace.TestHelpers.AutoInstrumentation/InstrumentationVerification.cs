// <copyright file="InstrumentationVerification.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.InstrumentedAssemblyGenerator;
using Datadog.InstrumentedAssemblyVerification;
using Xunit;

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
    /// Default is value is disabled.
    /// </summary>
    public const string CopyingOriginalModulesEnabled = "DD_COPY_ORIGINALS_MODULES_TO_DISK";

    public static void VerifyInstrumentation(Process process, string logDirectory)
    {
        var instrumentedLogsPath = FindInstrumentationLogsFolder(process, logDirectory);
        if (instrumentedLogsPath == null)
        {
            throw new Exception($"Unable to find instrumentation verification directory for process {process.Id}");
        }

        var copyOriginalsModulesToDisk = Environment.GetEnvironmentVariable(InstrumentationVerification.CopyingOriginalModulesEnabled);
        var generatorArgs = new AssemblyGeneratorArgs(instrumentedLogsPath, copyOriginalModulesToDisk: copyOriginalsModulesToDisk?.ToLower() is "true" or "1");
        var generatedModules = InstrumentedAssemblyGeneration.Generate(generatorArgs);

        var results = new List<VerificationOutcome>();
        foreach (var instrumentedAssembly in generatedModules)
        {
            var result = new VerificationsRunner(
                instrumentedAssembly.InstrumentedAssemblyPath,
                instrumentedAssembly.OriginalAssemblyPath,
                instrumentedAssembly.ModifiedMethods.Select(m => (m.TypeFullName, m.MethodAndArgumentsName)).ToList()).Run();
            results.Add(result);
        }

        Assert.True(
            results.TrueForAll(r => r.IsValid),
            "Instrumentation verification test failed:" + Environment.NewLine + string.Join(Environment.NewLine, results.Where(r => !r.IsValid).Select(r => r.FailureReason)));
    }

    public static string FindInstrumentationLogsFolder(Process process, string logsFolder)
    {
        var processExecutableFileName = Path.GetFileNameWithoutExtension(process.StartInfo.FileName);
        Assert.NotNull(logsFolder);
        var instrumentationLogsFolder = new DirectoryInfo(Path.Combine(logsFolder, InstrumentedAssemblyGeneratorConsts.InstrumentedAssemblyGeneratorLogsFolder));

        if (!instrumentationLogsFolder.Exists)
        {
            return null;
        }

        string pattern = $"{processExecutableFileName}_{process.Id}_*"; // * wildcard matches process start time
        var processSpecificInstrumentationLogsFolder =
            instrumentationLogsFolder
               .GetDirectories(pattern)
               .OrderBy(d => d.CreationTime)
               .LastOrDefault();

        return processSpecificInstrumentationLogsFolder?.FullName;
    }
}
