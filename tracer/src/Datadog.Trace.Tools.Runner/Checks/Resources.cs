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
        public const string NetFrameworkRuntime = "Target process is running with .NET Framework";
        public const string NetCoreRuntime = "Target process is running with .NET Core";
        public const string RuntimeDetectionFailed = "Failed to detect target process runtime, assuming .NET Framework";
        public const string BothRuntimesDetected = "The target process is running .NET Framework and .NET Core simultaneously. Checks will be performed assuming a .NET Framework runtime.";
        public const string ProfilerNotLoaded = "Profiler is not loaded into the process";
        public const string TracerNotLoaded = "Tracer is not loaded into the process";
        public const string AgentDetectionFailed = "Could not detect the agent version. It may be running with a version older than 7.27.0.";
        public const string IisProcess = "The target process is an IIS process. The detection of the configuration might be incomplete, it's recommended to use dd-trace check iis <site name> instead.";
        public const string MissingGac = "The Datadog.Trace assembly could not be found in the GAC. Make sure the tracer has been properly installed with the MSI.";
        public const string NoWorkerProcess = "No worker process found, to perform additional checks make sure the application is active";
        public const string GetProcessError = "Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.";
        public const string IisNoIssue = "No issue found with the IIS site.";
        public const string IisMixedRuntimes = "The application pool is configured to host both .NET Framework and .NET Core runtimes. When hosting .NET Core, it's recommended to set '.NET CLR Version' to 'No managed code' to prevent conflicts.";
        public const string OutOfProcess = "Detected ASP.NET Core hosted out of proces. Trying to find the application process.";
        public const string AspNetCoreProcessNotFound = "Could not find the ASP.NET Core applicative process.";
        public const string VersionConflict = "Tracer version 1.x can't be loaded simultaneously with other versions and will produce orphaned traces. Make sure to synchronize the Datadog.Trace NuGet version with the installed automatic instrumentation package version.";

        public static string EnvironmentVariableNotSet(string environmentVariable) => $"The environment variable {environmentVariable} is not set";

        public static string TracerHomeNotFoundFormat(string tracerHome) => $"DD_DOTNET_TRACER_HOME is set to '{tracerHome}' but the directory does not exist";

        public static string WrongEnvironmentVariableFormat(string key, string expectedValue, string? actualValue) => $"The environment variable {key} should be set to '{expectedValue}' (current value: {EscapeOrNotSet(actualValue)})";

        public static string DetectedAgentUrlFormat(string url) => $"Detected agent url: {url}. Note: this url may be incorrect if you configured the application through a configuration file.";

        public static string WrongStatusCodeFormat(HttpStatusCode statusCode) => $"Agent replied with wrong status code: {statusCode}";

        public static string DetectedAgentVersionFormat(string version) => $"Detected agent version {version}";

        public static string ErrorDetectingAgent(string url, string error) => $"Error connecting to Agent at {url}: {error}";

        public static string ConnectToEndpointFormat(string endpoint, string transport) => $"Connecting to Agent at endpoint {endpoint} using {transport}";

        public static string ErrorCheckingRegistry(string error) => $"Error trying to read the registry: {error}";

        public static string SuspiciousRegistryKey(string key) => $@"The registry key HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\{key} is defined and could prevent the tracer from working properly. Please check that all external profilers have been uninstalled properly.";

        public static string MissingRegistryKey(string key) => $@"The registry key {key} is missing. Make sure the tracer has been properly installed with the MSI.";

        public static string MissingProfilerRegistry(string key, string path) => $@"The registry key {key} was set to path '{path}' but the file is missing or you don't have sufficient permission. Try reinstalling the tracer with the MSI and check the permissions.";

        public static string MissingProfilerEnvironment(string key, string path) => $@"The environment variable {key} is set to {path} but the file is missing or you don't have sufficient permission.";

        public static string GacVersionFormat(string version) => $"Found Datadog.Trace version {version} in the GAC";

        public static string FetchingApplication(string site, string application) => $"Fetching application {application} from site {site}";

        public static string InspectingWorkerProcess(int pid) => $"Inspecting worker process {pid}";

        public static string ErrorExtractingConfiguration(string error) => $"Could not extract configuration from site: {error}";

        public static string AspNetCoreProcessFound(int pid) => $"Found ASP.NET Core applicative process: {pid}";

        public static string WrongProfilerRegistry(string registryKey, string actualProfiler) => $"The registry key {registryKey} was set to '{actualProfiler}' but it should point to 'Datadog.Trace.ClrProfiler.Native.dll'. Please check that all external profilers have been uninstalled properly and try reinstalling the tracer.";

        public static string CouldNotFindSite(string site, IEnumerable<string> availableSites)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Could not find site {site}");
            sb.AppendLine("Available sites:");

            foreach (var s in availableSites)
            {
                sb.AppendLine($" - {s}");
            }

            return sb.ToString();
        }

        public static string CouldNotFindApplication(string site, string application, IEnumerable<string> availableApplications)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Could not find application {application} in site {site}");
            sb.AppendLine("Available applications:");

            foreach (var app in availableApplications)
            {
                sb.AppendLine($" - {app}");
            }

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

        public static string WrongProfilerEnvironment(string environmentVariable, string actualProfiler)
        {
            var expectedProfiler = RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Datadog.Trace.ClrProfiler.Native.dll" : "Datadog.Trace.ClrProfiler.Native.so";

            return $"The environment variable {environmentVariable} was set to '{actualProfiler}' but it should point to '{expectedProfiler}'";
        }

        private static string EscapeOrNotSet(string? str) => str == null ? "not set" : $"'{str}'";
    }
}
