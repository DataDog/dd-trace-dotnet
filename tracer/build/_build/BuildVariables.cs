using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;

public static class BuildVariables
{
    public static void AddDebuggerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.AddProfilerEnvironmentVariables(tracerHomeDirectory);
        envVars.Add("DD_DEBUGGER_ENABLED", "1");
    }

    public static void AddProfilerEnvironmentVariables(this Dictionary<string, string> envVars, AbsolutePath tracerHomeDirectory)
    {
        envVars.Add("COR_ENABLE_PROFILING", "1");
        envVars.Add("COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
        envVars.Add("COR_PROFILER_PATH_32", tracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll");
        envVars.Add("COR_PROFILER_PATH_64", tracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll");
        envVars.Add("CORECLR_ENABLE_PROFILING", "1");
        envVars.Add("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
        envVars.Add("DD_INTEGRATIONS", tracerHomeDirectory / "integrations.json");
        envVars.Add("DD_DOTNET_TRACER_HOME", tracerHomeDirectory);


        if (EnvironmentInfo.IsWin)
        {
            envVars.Add("CORECLR_PROFILER_PATH_32", tracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll");
            envVars.Add("CORECLR_PROFILER_PATH_64", tracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll");
        }
        else
        {
            envVars.Add("CORECLR_PROFILER_PATH", tracerHomeDirectory / "Datadog.Trace.ClrProfiler.Native.so");
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
