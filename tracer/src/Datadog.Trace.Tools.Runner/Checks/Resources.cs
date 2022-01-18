// <copyright file="Resources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal static class Resources
    {
        public const string NetFrameworkRuntime = "Target process is running with .NET Framework";
        public const string NetCoreRuntime = "Target process is running with .NET Core";
        public const string RuntimeDetectionFailed = "Failed to detect target process runtime, assuming .NET Framework";
        public const string ProfilerNotLoaded = "Profiler is not loaded into the process";
        public const string TracerNotLoaded = "Tracer is not loaded into the process";
        public const string TracerHomeNotSet = "The environment variable DD_DOTNET_TRACER_HOME is not set";
        public const string AgentDetectionFailed = "Could not detect the agent version. It may be running with a version older than 7.27.0.";

        public static string TracerHomeNotFoundFormat(string tracerHome) => $"DD_DOTNET_TRACER_HOME is set to '{tracerHome}' but the directory does not exist";

        public static string WrongEnvironmentVariableFormat(string key, string expectedValue, string actualValue) => $"The environment variable {key} should be set to '{expectedValue}' (current value: {EscapeOrNotSet(actualValue)})";

        public static string DetectedAgentUrlFormat(string url) => $"Detected agent url: {url}. Note: this url may be incorrect if you configured the application through a configuration file.";

        public static string WrongStatusCodeFormat(HttpStatusCode statusCode) => $"Agent replied with wrong status code: {statusCode}";

        public static string DetectedAgentVersionFormat(string version) => $"Detected agent version {version}";

        public static string ErrorDetectingAgent(string url, string error) => $"Error while trying to reach agent at {url}: {error}";

        private static string EscapeOrNotSet(string str) => str == null ? "not set" : $"'{str}'";
    }
}
