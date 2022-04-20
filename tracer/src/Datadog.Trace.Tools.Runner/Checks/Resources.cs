// <copyright file="Resources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal static class Resources
    {
        public const string NetFrameworkRuntime = "Target process is running with .NET Framework.";
        public const string NetCoreRuntime = "Target process is running with .NET Core.";
        public const string RuntimeDetectionFailed = "Failed to detect target process runtime, assuming .NET Framework.";
        public const string BothRuntimesDetected = "The target process is running .NET Framework and .NET Core simultaneously. Checks will be performed assuming a .NET Framework runtime.";
        public const string ProfilerNotLoaded = "The tracer native library is not loaded into the process.";
        public const string TracerNotLoaded = "The tracer managed assembly is not loaded into the process.";
        public const string AgentDetectionFailed = "Could not detect the agent version. It may be running with a version older than 7.27.0.";
        public const string IisProcess = "The target process is an IIS process. The detection of the configuration might be incomplete. It's recommended to use \"dd-trace check iis <site name>\" instead.";
        public const string MissingGac = "The Datadog.Trace assembly could not be found in the GAC. Make sure the tracer has been properly installed with the MSI.";
        public const string NoWorkerProcess = "No worker process found. To perform additional checks, make sure the application is active.";
        public const string GetProcessError = "Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.";
        public const string IisNoIssue = "No issue found with the IIS site.";
        public const string IisMixedRuntimes = "The IIS Application Pool is configured to host both .NET Framework and .NET Core runtimes. When hosting .NET Core, it's recommended to set \".NET CLR Version\" to \"No managed code\" to avoid conflicts.";
        public const string OutOfProcess = "Detected ASP.NET Core hosted out-of-process. Trying to find the application process.";
        public const string AspNetCoreProcessNotFound = "Could not find the ASP.NET Core application process.";
        public const string VersionConflict = "Tracer version 1.x can't be loaded simultaneously with other versions and will produce orphaned traces. Make sure to synchronize the Datadog.Trace NuGet version with the installed automatic instrumentation package version.";

        public static string NativeLibraryVersion(string version) => $"The tracer native library version {version} is loaded into the process.";

        public static string ManagedLibraryVersion(string version) => $"The tracer managed assembly version {version} is loaded into the process.";

        public static string EnvironmentVariableNotSet(string environmentVariable) => $"The environment variable \"{environmentVariable}\" is not set.";

        public static string EnvironmentVariableNotSet(IEnumerable<string> environmentVariables) => $"None of the environment variables \"{string.Join("\" or \"", environmentVariables)}\" are set. At least one of these is required.";

        public static string TracerHomeNotFoundFormat(string tracerHome) => $"\"DD_DOTNET_TRACER_HOME\" is set to \"{tracerHome}\" but the directory does not exist.";

        public static string WrongEnvironmentVariableFormat(string key, string expectedValue, string? actualValue) => $"The environment variable \"{key}\" is set to \"{actualValue}\". Expected \"{expectedValue}\".";

        public static string DetectedAgentUrlFormat(string url) => $"Detected Agent URL: {url}. Note: this URL may be incorrect if you configured the application through a configuration file.";

        public static string WrongStatusCodeFormat(HttpStatusCode statusCode) => $"Agent replied with wrong status code: {statusCode}";

        public static string DetectedAgentVersionFormat(string version) => $"Detected Agent version {version}";

        public static string ErrorDetectingAgent(string url, string error) => $"Error connecting to Agent at {url}: {error}";

        public static string ConnectToEndpointFormat(string endpoint, string transport) => $"Connecting to Agent at endpoint {endpoint} using {transport}";

        public static string ErrorCheckingRegistry() => "Error trying to read the Windows Registry.";

        public static string SuspiciousRegistryKey(string parentKey, string key) => $@"The registry key ""HKEY_LOCAL_MACHINE\{parentKey}\{key}"" is defined and could prevent the tracer from working properly. Please check that all external profilers have been uninstalled properly.";

        public static string MissingRegistryKey(string key) => $"The registry key \"{key}\" is missing. Make sure the tracer has been properly installed with the MSI.";

        public static string FileNameSource(ValueSource source) => source switch
                                                                          {
                                                                              ValueSource.EnvironmentVariable => "environment variable",
                                                                              ValueSource.WindowsRegistry => "registry key",
                                                                              _ => "unknown"
                                                                          };

        public static string WrongNativeLibrary(ValueSource source, string key, string actualPath, string expectedFileName) => $"The {FileNameSource(source)} \"{key}\" is set to \"{actualPath}\", but it should point to \"{expectedFileName}\". Please check that all external profilers have been uninstalled properly and try reinstalling the tracer.";

        public static string MissingNativeLibrary(ValueSource source, string key, string path) => $"The {FileNameSource(source)} \"{key}\" is set to \"{path}\", but the file is missing or you don't have sufficient permissions.";

        public static string DetectedNativeLibraryArchitecture(string nativePath, Architecture nativeArchitecture) => $"Detected architecture {nativeArchitecture} in native tracing library \"{nativePath}\".";

        public static string MismatchedArchitecture(string nativePath, Architecture nativeArchitecture, Architecture processArchitecture) => $"The process architecture is {processArchitecture}, but the architecture of tracing library \"{nativePath}\" is {nativeArchitecture}. Tracing library architecture must match the running process.";

        public static string CannotDetermineNativeLibraryArchitecture(string path) => $"Unable to determine architecture of tracing library \"{path}\".";

        public static string CannotDetermineProcessArchitecture() => "Unable to determine process architecture.";

        public static string DetectedProcessArchitecture(Architecture architecture) => $"Detected process architecture is {architecture}.";

        public static string GacVersionFormat(string version) => $"Found Datadog.Trace assembly version {version} in the GAC.";

        public static string FetchingApplication(string site, string application) => $"Fetching IIS application \"{site}{application}\".";

        public static string InspectingWorkerProcess(int pid) => $"Inspecting worker process {pid}.";

        public static string ErrorExtractingConfiguration(string error) => $"Could not extract configuration from site: {error}";

        public static string AspNetCoreProcessFound(int pid) => $"Found ASP.NET Core application process: {pid}.";

        public static string IisApplicationNotProvided() => "IIS application name not provided. ";

        public static string CouldNotFindIisApplication(string site, string application) => $"Could not find IIS application \"{site}{application}\". ";

        public static string ListAllIisApplications(IEnumerable<string> availableApplications)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available IIS applications:");

            foreach (var app in availableApplications)
            {
                sb.AppendLine($" - {app}");
            }

            sb.AppendLine();
            sb.AppendLine("USAGE:");
            sb.AppendLine("    dd-trace check iis [siteName]");

            return sb.ToString();
        }

        public static string MultipleTracers(IEnumerable<string> versions)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Found multiple instances of Datadog.Trace.dll in the target process.");
            sb.AppendLine("Detected versions:");

            // The ordering is not required but makes the output consistent for tests
            foreach (var version in versions.OrderBy(v => v))
            {
                sb.AppendLine($"- {version}");
            }

            return sb.ToString();
        }
    }
}
