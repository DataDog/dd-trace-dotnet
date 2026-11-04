// <copyright file="PublishResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;

namespace Datadog.Trace.Bundle.ArtifactTests;

public class PublishResult
{
    public PublishResult(int exitCode, string output, string datadogDirectory)
    {
        ExitCode = exitCode;
        Output = output;
        DatadogDirectory = datadogDirectory;
    }

    public int ExitCode { get; }

    public string Output { get; }

    public string DatadogDirectory { get; }

    public string[] GetDirectories()
        => Directory.Exists(DatadogDirectory)
               ? Directory.GetDirectories(DatadogDirectory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray()
               : [];

    public string[] GetFiles(string directory)
        => Directory.GetFiles(Path.Combine(DatadogDirectory, directory)).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public override string ToString() => $"Exit code {ExitCode}{Environment.NewLine}{Output}";
}
