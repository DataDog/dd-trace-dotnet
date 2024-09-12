using System.Collections.Generic;
using System.IO;
using Nuke.Common;

partial class Build
{
    public void AddDebuggerEnvironmentVariables(Dictionary<string, string> envVars, ExplorationTestDescription description, TargetFramework framework)
    {
        AddTracerEnvironmentVariables(envVars);

        if (DisableDynamicInstrumentationProduct)
        {
            return;
        }

        envVars.Add("DD_DYNAMIC_INSTRUMENTATION_ENABLED", "1");
        envVars.Add("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL", "1");

        if (description.LineProbesEnabled)
        {
            envVars.Add("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL_LINES", "1");
            var testRootPath = description.GetTestTargetPath(ExplorationTestsDirectory, framework, BuildConfiguration);
            envVars.Add("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL_LINES_PATH", Path.Combine(testRootPath, LineProbesFileName));
        }

        envVars.Add("COMPlus_DbgEnableMiniDump", "1");
        envVars.Add("COMPlus_DbgMiniDumpType", "4");
        envVars.Add("VSTEST_CONNECTION_TIMEOUT", "200");

        if (EnableFaultTolerantInstrumentation)
        {
            envVars.Add("DD_INTERNAL_FAULT_TOLERANT_INSTRUMENTATION_ENABLED", "true");
        }

        // Uncomment to get rejit verify files
        // envVars.Add("DD_WRITE_INSTRUMENTATION_TO_DISK", "1");
        // envVars.Add("CopyingOriginalModulesEnabled", "1");
    }

    public void AddContinuousProfilerEnvironmentVariables(Dictionary<string, string> envVars)
    {
        AddTracerEnvironmentVariables(envVars);
    }

    public void AddTracerEnvironmentVariables(Dictionary<string, string> envVars)
    {
        envVars.Add("COR_ENABLE_PROFILING", "1");
        envVars.Add("COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");

        envVars.Add("DD_DOTNET_TRACER_HOME", MonitoringHomeDirectory);

        envVars.Add("CORECLR_ENABLE_PROFILING", "1");
        envVars.Add("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");

        if (EnvironmentInfo.IsWin)
        {
            var loaderPath32 = MonitoringHomeDirectory / "win-x86" / $"{FileNames.NativeLoader}.dll";
            var loaderPath64 = MonitoringHomeDirectory / "win-x64" / $"{FileNames.NativeLoader}.dll";
            envVars.Add("COR_PROFILER_PATH_32", loaderPath32);
            envVars.Add("COR_PROFILER_PATH_64", loaderPath64);
            envVars.Add("CORECLR_PROFILER_PATH_32", loaderPath32);
            envVars.Add("CORECLR_PROFILER_PATH_64", loaderPath64);
        }
        else
        {
            var (arch, ext) = GetUnixArchitectureAndExtension();
            envVars.Add("CORECLR_PROFILER_PATH", MonitoringHomeDirectory / arch / $"{FileNames.NativeLoader}.{ext}");
        }
    }

    public void AddExtraEnvVariables(Dictionary<string, string> envVars, string[] extraEnvVars)
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
