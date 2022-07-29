using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;

public static class BuildVariables
{
    public static void AddDebuggerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.AddTracerEnvironmentVariables(tracerHomeDirectory);
        envVars.Add("DD_INTERNAL_DEBUGGER_ENABLED", "1");
        envVars.Add("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL", "1");
    }

    public static void AddContinuousProfilerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.AddTracerEnvironmentVariables(tracerHomeDirectory);
    }

    public static void AddTracerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath monitoringHomeDirectory)
    {
        envVars.Add("COR_ENABLE_PROFILING", "1");
        envVars.Add("COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");

        envVars.Add("COR_PROFILER_PATH_32", monitoringHomeDirectory / "Datadog.AutoInstrumentation.NativeLoader.x86.dll");
        envVars.Add("COR_PROFILER_PATH_64", monitoringHomeDirectory / "Datadog.AutoInstrumentation.NativeLoader.x64.dll");

        if (EnvironmentInfo.IsWin)
        {
            envVars.Add("DD_DOTNET_TRACER_HOME", monitoringHomeDirectory / "tracer");
        }
        else
        {
            envVars.Add("DD_DOTNET_TRACER_HOME", monitoringHomeDirectory);
        }

        envVars.Add("CORECLR_ENABLE_PROFILING", "1");
        envVars.Add("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");

        if (EnvironmentInfo.IsWin)
        {
            envVars.Add("CORECLR_PROFILER_PATH_32", monitoringHomeDirectory / "Datadog.AutoInstrumentation.NativeLoader.x86.dll");
            envVars.Add("CORECLR_PROFILER_PATH_64", monitoringHomeDirectory / "Datadog.AutoInstrumentation.NativeLoader.x64.dll");
        }
        else
        {
            envVars.Add("CORECLR_PROFILER_PATH", monitoringHomeDirectory / "Datadog.Trace.ClrProfiler.Native.so");
        }
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
}
