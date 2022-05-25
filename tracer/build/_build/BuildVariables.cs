using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;

public static class BuildVariables
{
    public static void AddDebuggerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.AddCommonEnvironmentVariables(tracerHomeDirectory);
        envVars.Add("DD_DEBUGGER_ENABLED", "1");
    }

    public static void AddContinuousProfilerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.AddCommonEnvironmentVariables(tracerHomeDirectory);
        envVars.Add("DD_PROFILING_ENABLED", "1");
    }

    public static void AddTracerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath monitoringHome)
    {
        envVars.AddCommonEnvironmentVariables(monitoringHome);
        envVars.Add("DD_DOTNET_TRACER_HOME", monitoringHome);
    }

    public static void AddExtraEnvVariables(this Dictionary<string, string> envVars, string[] extraEnvVars)
    {
        if (extraEnvVars == null || extraEnvVars.Length == 0)
        {
            return;
        }

        foreach (var envVar in extraEnvVars)
        {
            var kvp = envVar.Split('=');
            envVars[kvp[0]] = kvp[1];
        }
    }

    private static void AddCommonEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath monitoringHome)
    {
        envVars.Add("COR_ENABLE_PROFILING", "1");
        envVars.Add("COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
        envVars.Add("COR_PROFILER_PATH_32", monitoringHome / "Datadog.Trace.ClrProfiler.Native.x86.dll");
        envVars.Add("COR_PROFILER_PATH_64", monitoringHome / "Datadog.Trace.ClrProfiler.Native.x64.dll");
        envVars.Add("CORECLR_ENABLE_PROFILING", "1");
        envVars.Add("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");


        if (EnvironmentInfo.IsWin)
        {
            envVars.Add("CORECLR_PROFILER_PATH_32", monitoringHome / "Datadog.Trace.ClrProfiler.Native.x86.dll");
            envVars.Add("CORECLR_PROFILER_PATH_64", monitoringHome / "Datadog.Trace.ClrProfiler.Native.x64.dll");
        }
        else
        {
            envVars.Add("CORECLR_PROFILER_PATH", monitoringHome / "Datadog.Trace.ClrProfiler.Native.so");
        }
    }
}
