// <copyright file="BundleProjectFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.Bundle.ArtifactTests;

/// <summary>
/// Creates a console app outside the repository (so the repo's Directory.Build.props and global.json don't apply)
/// that references the built Datadog.Trace.Bundle package, using an isolated NuGet cache so a previously cached
/// package with the same version is never used.
/// </summary>
public sealed class BundleProjectFixture : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "dd-bundle-tests-" + Path.GetRandomFileName());

    public BundleProjectFixture()
    {
        // Set by the build to where it creates the packages, and where CI downloads them to. Falls back to the
        // default location when running from the IDE
        var nugetDirectory = Environment.GetEnvironmentVariable("NugetArtifactsDirectory");
        if (string.IsNullOrEmpty(nugetDirectory))
        {
            nugetDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "artifacts", "output", "nuget");
        }

        var projectDirectory = Path.Combine(_rootDirectory, "app");
        Directory.CreateDirectory(projectDirectory);

        // Source mapping ensures the Datadog packages only come from the build output, never from nuget.org
        var nugetConfig = $"""
                           <?xml version="1.0" encoding="utf-8"?>
                           <configuration>
                             <packageSources>
                               <clear />
                               <add key="tracer" value="{nugetDirectory}" />
                               <add key="bundle" value="{Path.Combine(nugetDirectory, "bundle")}" />
                               <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                             </packageSources>
                             <packageSourceMapping>
                               <packageSource key="tracer"><package pattern="Datadog.Trace" /></packageSource>
                               <packageSource key="bundle"><package pattern="Datadog.Trace.Bundle" /></packageSource>
                               <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                             </packageSourceMapping>
                           </configuration>
                           """;

        var project = """
                      <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                          <OutputType>Exe</OutputType>
                          <TargetFrameworks>net8.0;net5.0;netcoreapp2.1;netcoreapp2.0;net48</TargetFrameworks>
                        </PropertyGroup>
                        <ItemGroup>
                          <PackageReference Include="Datadog.Trace.Bundle" Version="*-*" />
                        </ItemGroup>
                      </Project>
                      """;

        File.WriteAllText(Path.Combine(projectDirectory, "nuget.config"), nugetConfig);
        File.WriteAllText(Path.Combine(projectDirectory, "App.csproj"), project);
        File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"), "class Program { static void Main() { } }");
    }

    /// <summary>
    /// Runs dotnet publish with the provided space-separated arguments. Each argument is passed verbatim,
    /// so quotes are seen by MSBuild, as when running <c>dotnet publish '-p:DatadogPlatforms="win-x64;win-x86"'</c> in a shell.
    /// </summary>
    public PublishResult Publish(string arguments)
    {
        var outputDirectory = Path.Combine(_rootDirectory, "out", Path.GetRandomFileName());

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(_rootDirectory, "app"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Build servers are disabled so no MSBuild nodes are left holding files in the temp directory
        string[] allArguments = ["publish", "--disable-build-servers", "-c", "Release", "-o", outputDirectory, .. arguments.Split(' ')];
        foreach (var argument in allArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.EnvironmentVariables["NUGET_PACKAGES"] = Path.Combine(_rootDirectory, "packages");
        startInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo);
        using var helper = new ProcessHelper(process);

        // Drain only waits for the output streams to close, so also wait for the exit before reading the exit code.
        // The timeout ensures a hang fails the test with the output, instead of timing out the CI job without logs
        var timeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
        if (!helper.Drain(timeout) || !process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet publish {arguments} did not complete in time. Output:{Environment.NewLine}{helper.StandardOutput}{helper.ErrorOutput}");
        }

        return new PublishResult(process.ExitCode, helper.StandardOutput + helper.ErrorOutput, Path.Combine(outputDirectory, "datadog"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
        catch
        {
            // Best effort
        }
    }
}
