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

internal static class InstrumentationVerification
{
    /// <summary>
    /// Configuration key for enabling or disabling the instrumentation verification.
    /// Default is value is disabled.
    /// </summary>
    public const string InstrumentationVerificationEnabled = "DD_WRITE_INSTRUMENTATION_TO_DISK";

    public static void VerifyInstrumentation(Process process, string logDirectory)
    {
        var instrumentedLogsPath = GetInstrumentationLogsFolder(process, logDirectory);

        var generatorArgs = new AssemblyGeneratorArgs(instrumentedLogsPath);
        var generatedModules = InstrumentedAssemblyGeneration.Generate(generatorArgs);

        var results = new List<VerificationOutcome>();
        foreach (var (modulePath, methods) in generatedModules)
        {
            var moduleName = Path.GetFileName(modulePath);
            var originalModulePath = Path.Combine(instrumentedLogsPath, InstrumentedAssemblyGeneratorConsts.OriginalModulesFolderName, moduleName);
            var result = new VerificationsRunner(
                modulePath,
                originalModulePath,
                methods).Run();
            results.Add(result);
        }

        Assert.True(
            results.TrueForAll(r => r.IsValid),
            "Instrumentation verification test failed:" + Environment.NewLine + string.Join(Environment.NewLine, results.Where(r => !r.IsValid).Select(r => r.FailureReason)));
    }

    private static string GetInstrumentationLogsFolder(Process process, string logsFolder)
    {
        var processExecutableFileName = Path.GetFileNameWithoutExtension(process.StartInfo.FileName);
        Assert.NotNull(logsFolder);
        var instrumentationLogsFolder = new DirectoryInfo(Path.Combine(logsFolder, InstrumentedAssemblyGeneratorConsts.InstrumentedAssemblyGeneratorLogsFolder));

        if (!instrumentationLogsFolder.Exists)
        {
            throw new Exception($"Unable to find instrumentation verification directory at {instrumentationLogsFolder}");
        }

        string pattern = $"{processExecutableFileName}_{process.Id}_*"; // * wildcard matches process start time
        var processSpecificInstrumentationLogsFolder =
            instrumentationLogsFolder
               .GetDirectories(pattern)
               .OrderBy(d => d.CreationTime)
               .LastOrDefault();

        if (processSpecificInstrumentationLogsFolder == null)
        {
            throw new Exception($"Unable to find instrumentation verification directory that matches pattern '{pattern}' in {instrumentationLogsFolder}");
        }

        return processSpecificInstrumentationLogsFolder.FullName;
    }
}
