// <copyright file="Resources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.Tools.dd_dotnet.Checks
{
    internal static class Resources
    {
        public const string NetFrameworkRuntime = "Target process is running with .NET Framework";
        public const string NetCoreRuntime = "Target process is running with .NET Core";
        public const string RuntimeDetectionFailedWindows = "Failed to detect target process runtime, assuming .NET Framework";
        public const string RuntimeDetectionFailedLinux = "Failed to detect target process runtime, assuming .NET Core";
        public const string BothRuntimesDetected = "The target process is running .NET Framework and .NET Core simultaneously. Checks will be performed assuming a .NET Framework runtime.";
        public const string LoaderNotLoaded = "The native loader library is not loaded into the process";
        public const string NativeTracerNotLoaded = "The native tracer library is not loaded into the process";
        public const string TracerNotLoaded = "Tracer is not loaded into the process";
        public const string AgentDetectionFailed = "Could not detect the agent version. It may be running with a version older than 7.27.0.";
        public const string IisProcess = "The target process is an IIS process. The detection of the configuration might be incomplete, please use dd-trace check iis <site name> instead.";
        public const string MissingGac = "The Datadog.Trace assembly could not be found in the GAC. Make sure the tracer has been properly installed with the MSI.";
        public const string NoWorkerProcess = "No worker process found, to perform additional checks make sure the application is active";
        public const string IisNoIssue = "No issue found with the IIS site.";
        public const string IisMixedRuntimes = "The application pool is configured to host both .NET Framework and .NET Core runtimes. When hosting .NET Core, it's recommended to set '.NET CLR Version' to 'No managed code' to prevent conflict: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-3.1#create-the-iis-site:~:text=CLR%20version%20to-,No%20Managed%20Code,-%3A";
        public const string OutOfProcess = "Detected ASP.NET Core hosted out of proces. Trying to find the application process.";
        public const string AspNetCoreProcessNotFound = "Could not find the ASP.NET Core applicative process.";
        public const string AspNetCoreOutOfProcessNotFound = "ASP.NET Core has been detected but no .NET runtime. It could mean that the application is using out-of-process hosting and hasn't received a request to complete the initialization. Please make sure that the site has received at least one web request since the last time it was restarted.";
        public const string VersionConflict = "Tracer version 1.x can't be loaded simultaneously with other versions and will produce orphaned traces. Make sure to synchronize the Datadog.Trace NuGet version with the installed automatic instrumentation package version.";
        public const string IisExpressWorkerProcess = "Cannot detect the worker process when using IIS Express. Use the --workerProcess option to manually provide it.";
        public const string IisNotFound = "Could not find IIS. Make sure IIS is properly installed and enable, and run the tool from an elevated prompt.";

        public const string TracingWithBundleProfilerPath = "Check failing with Datadog.Trace.Bundle Nuget, related documentation: https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=nuget#install-the-tracer";
        public const string TracingWithInstallerWindowsNetFramework = "Installer/MSI related documentation: https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-framework?tab=windows#install-the-tracer";
        public const string TracingWithInstallerWindowsNetCore = "Installer/MSI related documentation: https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=windows#install-the-tracer";
        public const string TracingWithInstallerLinux = "Installer related documentation: https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core?tab=linux#install-the-tracer";
        public const string TraceProgramNotFound = "Unable to find Datadog .NET Tracer program, make sure the tracer has been properly installed with the MSI.";

        public const string TraceEnabledNotSet = "DD_TRACE_ENABLED is not set, the default value is true.";
        public const string SetupChecks = "---- STARTING TRACER SETUP CHECKS -----";
        public const string ConfigurationChecks = "---- CONFIGURATION CHECKS -----";
        public const string DdAgentChecks = "---- DATADOG AGENT CHECKS -----";

        public const string ContinuousProfilerEnabled = "DD_PROFILING_ENABLED is set.";
        public const string ContinuousProfilerEnabledWithHeuristics = "DD_PROFILING_ENABLED is set to 'auto'. The continuous profiler is enabled and may begin profiling based on heuristics.";
        public const string ContinuousProfilerSsiEnabledWithHeuristics = "DD_INJECTION_ENABLED contains 'profiler'. The continuous profiler is enabled through SSI and may begin profiling based on heuristics.";
        public const string ContinuousProfilerSsiMonitoring = "DD_INJECTION_ENABLED is set but does not contain 'profiler'. The continuous profiler is monitoring but will not generate profiles.";
        public const string ContinuousProfilerDisabled = "The continuous profiler is explicitly disabled through DD_PROFILING_ENABLED.";
        public const string ContinuousProfilerNotSet = "DD_INJECTION_ENABLED and DD_PROFILING_ENABLED are not set, the continuous profiler is disabled.";
        public const string ContinuousProfilerNotLoaded = "The continuous profiler library is not loaded into the process.";
        public const string ContinuousProfilerWithoutLoader = "The continuous profiler needs the Datadog.Trace.ClrProfiler.Native module and the loader.conf file to work. Try reinstalling the tracer in version 2.14+.";

        public const string LdPreloadNotSet = "The environment variable LD_PRELOAD is not set. Check the Datadog .NET Profiler documentation to set it properly.";

        private static int _checkNumber = 1;

        public static void ResetChecks() => _checkNumber = 1;

        public static string EnableDiagnosticsSet(string key) => $"The environment variable {key} is set to 0, which disables profiling. No tracing, profiling, or security data will be collected until is set to 1.";

        public static string GetProcessError(string error) => $"Could not fetch information about target process: {error}. Make sure to run the command from an elevated prompt, and check that the pid is correct.";

        public static string TracerNotEnabled(string value) => $"Tracing is explicitly disabled through DD_TRACE_ENABLED with a value of {value}, to enable automatic tracing set it to true.";

        public static string ApiWrapperNotFound(string path) => $"The environment variable LD_PRELOAD is set to '{path}' but the file could not be found. Check the Datadog .NET Profiler documentation to set it properly.";

        public static string WrongLdPreload(string path) => $"The environment variable LD_PRELOAD is set to '{path}' but it should point to Datadog.Linux.ApiWrapper.x64.so instead. Check the Datadog .NET Profiler documentation to set it properly.";

        public static string ProfilerVersion(string version) => $"The native library version {version} is loaded into the process.";

        public static string TracerVersion(string version) => $"The tracer version {version} is loaded into the process.";

        public static string EnvironmentVariableNotSet(string environmentVariable) => $"The environment variable {environmentVariable} is not set.";

        public static string TracerHomeNotFoundFormat(string tracerHome) => $"DD_DOTNET_TRACER_HOME is set to '{tracerHome}' but the directory does not exist.";

        public static string TracerHomeFoundFormat(string tracerHome) => $"DD_DOTNET_TRACER_HOME is set to '{tracerHome}' and the directory was found correctly.";

        public static string WrongEnvironmentVariableFormat(string key, string expectedValue, string? actualValue) => $"The environment variable {key} should be set to '{expectedValue}' (current value: {EscapeOrNotSet(actualValue)})";

        public static string DetectedAgentUrlFormat(string url) => $"Detected agent url: {url}. Note: this url may be incorrect if you configured the application through a configuration file.";

        public static string WrongStatusCodeFormat(int statusCode) => $"Agent replied with wrong status code: {statusCode}";

        public static string DetectedAgentVersionFormat(string version) => $"Detected agent version {version}";

        public static string ErrorDetectingAgent(string url, string error) => $"Error connecting to Agent at {url}: {error}";

        public static string ConnectToEndpointFormat(string endpoint, string transport) => $"Connecting to Agent at endpoint {endpoint} using {transport}";

        public static string ErrorCheckingRegistry(string error) => $"Error trying to read the registry: {error}";

        public static string SuspiciousRegistryKey(string parentKey, string key) => $@"The registry key HKEY_LOCAL_MACHINE\{parentKey}\{key} is defined and could prevent the tracer from working properly. Please check that all external profilers have been uninstalled properly.";

        public static string MissingRegistryKey(string key) => $@"The registry key {key} is missing. If using the MSI, make sure the installation was completed correctly try to repair/reinstall it.";

        public static string MissingProfilerRegistry(string key, string path) => $@"The registry key {key} was set to path '{path}' but the file is missing or you don't have sufficient permission. Try reinstalling the tracer with the MSI and check the permissions.";

        public static string MissingProfilerEnvironment(string key, string path) => $@"The environment variable {key} is set to {path} but the file is missing or you don't have sufficient permission.";

        public static string CorrectlySetupEnvironment(string key, string value) => $@"The environment variable {key} is set to the correct value of {value}.";

        public static string GacVersionFormat(string version) => $"Found Datadog.Trace version {version} in the GAC";

        public static string FetchingApplication(string site, string application) => $"Fetching IIS application \"{site}{application}\".";

        public static string InspectingWorkerProcess(int pid) => $"Inspecting worker process {pid}";

        public static string ErrorExtractingConfiguration(string error) => $"Could not extract configuration from site: {error}";

        public static string AspNetCoreProcessFound(int pid) => $"Found ASP.NET Core applicative process: {pid}";

        public static string WrongProfilerRegistry(string registryKey, string actualProfiler) => $"The registry key {registryKey} was set to '{actualProfiler}' but it should point to 'Datadog.Trace.ClrProfiler.Native.dll'. Please check that all external profilers have been uninstalled properly and try reinstalling the tracer.";

        public static string IisApplicationNotProvided() => "IIS application name not provided. ";

        public static string CouldNotFindIisApplication(string site, string application) => $"Could not find IIS application \"{site}{application}\". ";

        public static string IisManagerInitializationError(string error) => $"Could not initialize IIS manager: {error} Try to run the tool in administrator mode.";

        public static string IisWorkerProcessError(string error) => $"Could not detect the worker process: {error} Note that you must run the tool from an elevated prompt.";

        public static string ListAllIisApplications(IEnumerable<string> availableApplications)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available IIS applications:");

            foreach (var app in availableApplications)
            {
                sb.AppendLine($" {app}");
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
                sb.AppendLine($"{version}");
            }

            return sb.ToString();
        }

        public static string WrongProfilerEnvironment(string environmentVariable, string actualProfiler)
        {
            var expectedProfiler = RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Datadog.Trace.ClrProfiler.Native.dll" : "Datadog.Trace.ClrProfiler.Native.so";

            return $"The environment variable {environmentVariable} was set to '{actualProfiler}' but it should point to '{expectedProfiler}'";
        }

        public static string TracerProgramFound(string tracerProgramName) => $"{tracerProgramName} found in the installed programs.";

        public static string WrongTracerArchitecture(string tracerArchitecture) => $"Found {tracerArchitecture} installed but the current process is 64 Bit, make sure to install the 64-bit tracer instead.";

        public static string AppPoolCheckFindings(string appPool) => $"Initial check run did not pass, surfacing the incorrect configuration on the {appPool} AppPool:";

        public static string WrongLinuxFolder(string expected, string found) => $"Unable to find expected {expected} folder, found {found} instead, make sure to use the correct installer.";

        public static string UnsupportedLinuxArchitecture(string osArchitecture) => $"The Linux architecture: {osArchitecture} is not supported by the tracer, check: https://docs.datadoghq.com/tracing/trace_collection/compatibility/dotnet-core/#supported-processor-architectures ";

        public static string ErrorCheckingLinuxDirectory(string error) => $"Error trying to check the Linux installer directory: {error}";

        public static string EnvVarCheck(string envVar) => $"{_checkNumber++}. Checking {envVar} and related configuration value:";

        public static string ModuleCheck() => $"{_checkNumber++}. Checking Modules Needed so the Tracer Loads:";

        public static string TracerCheck() => $"{_checkNumber++}. Checking if process tracing configuration matches Installer or Bundler:";

        public static string TraceEnabledCheck() => $"{_checkNumber++}. Checking if tracing is disabled using DD_TRACE_ENABLED.";

        public static string ContinuousProfilerCheck() => $"{_checkNumber++}. Checking if profiling is enabled using DD_PROFILING_ENABLED.";

        public static string CorrectLinuxDirectoryFound(string path) => $"Found the expected path {path} based on the current OS Architecture.";

        private static string EscapeOrNotSet(string? str) => str == null ? "not set" : $"'{str}'";
    }
}
