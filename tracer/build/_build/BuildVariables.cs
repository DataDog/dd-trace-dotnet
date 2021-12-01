using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;

public partial class Build
{
    static Dictionary<string, string> GetDebuggerEnvironmentVariables(AbsolutePath tracerHomeDirectory)
    {
        var envVars = GetProfilerEnvironmentVariables(tracerHomeDirectory);
        envVars.Add("DD_DEBUGGER_ENABLED", "1");

        return envVars;
    }

    static Dictionary<string, string> GetProfilerEnvironmentVariables(AbsolutePath tracerHomeDirectory)
    {
        var envVars = new Dictionary<string, string>()
        {
            {"COR_ENABLE_PROFILING", "1"},
            {"COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
            {"COR_PROFILER_PATH_32", tracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll"},
            {"COR_PROFILER_PATH_64", tracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll"},
            {"CORECLR_ENABLE_PROFILING", "1"},
            {"CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
            {"DD_INTEGRATIONS", tracerHomeDirectory / "integrations.json" },
            {"DD_DOTNET_TRACER_HOME", tracerHomeDirectory },
        };

        if (EnvironmentInfo.IsWin)
        {
            envVars.Add("CORECLR_PROFILER_PATH_32", tracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll");
            envVars.Add("CORECLR_PROFILER_PATH_64", tracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll");
        }
        else
        {
            envVars.Add("CORECLR_PROFILER_PATH", tracerHomeDirectory / "Datadog.Trace.ClrProfiler.Native.so");
        }

        return envVars;
    }

    void AddExtraEnvVariables(Dictionary<string, string> envVars)
    {
        if (!(ExtraEnvVars?.Length > 0))
        {
            return;
        }

        foreach (var envVar in ExtraEnvVars)
        {
            var kvp = envVar.Split('=');
            envVars[kvp[0]] = kvp[1];
        }
    }
}
