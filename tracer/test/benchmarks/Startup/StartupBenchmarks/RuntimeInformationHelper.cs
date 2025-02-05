using System;
using System.Runtime.InteropServices;

namespace StartupBenchmarks;

public static class RuntimeInformationHelper
{
    public static string OSPlatform { get; } = GetOSPlatform();

    public static string ProcessArchitecture { get; } = GetProcessArchitecture();

    private static string GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return "win";
        }

        if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return "osx";
        }

        throw new PlatformNotSupportedException($"OS platform not supported: {RuntimeInformation.OSDescription}");
    }

    private static string GetProcessArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Process architecture not supported: {RuntimeInformation.OSArchitecture}"),
        };
    }
}
