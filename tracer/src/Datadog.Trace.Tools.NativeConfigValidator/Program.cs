// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.Trace.Tools.NativeConfigValidator;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var supportedConfigurationsPath = Path.Combine(repoRoot, "tracer", "src", "Datadog.Trace", "Configuration", "supported-configurations.yaml");

        if (!File.Exists(supportedConfigurationsPath))
        {
            Console.Error.WriteLine($"Could not find supported-configurations.yaml at {supportedConfigurationsPath}. Pass the repository root as the first argument.");
            return 2;
        }

        try
        {
            return new NativeConfigValidator().Validate(repoRoot, supportedConfigurationsPath) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Native configuration validation could not run: {ex.Message}");
            return 2;
        }
    }
}
