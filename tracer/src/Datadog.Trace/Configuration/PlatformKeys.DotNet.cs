// <copyright file="PlatformKeys.DotNet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    /// <summary>
    /// Platform key indicating the path to the .NET Core CLR profiler path
    /// </summary>
    public const string DotNetCoreClrProfiler = "CORECLR_PROFILER_PATH";
    public const string DotNetCoreClrProfiler64 = "CORECLR_PROFILER_PATH_64";
    public const string DotNetCoreClrProfiler32 = "CORECLR_PROFILER_PATH_32";

    /// <summary>
    /// Platform key indicating the path to the .NET Framework CLR profiler path
    /// </summary>
    public const string DotNetClrProfiler = "COR_PROFILER_PATH";
    public const string DotNetClrProfiler64 = "COR_PROFILER_PATH_64";
    public const string DotNetClrProfiler32 = "COR_PROFILER_PATH_32";

    /// <summary>
    /// COMPlus_ForceEnc is a .NET runtime environment variable that forces the CLR to enable
    /// Edit and Continue (EnC) support even in scenarios where it would normally be disabled.
    /// </summary>
    public const string ForceEnc = "COMPLUS_ForceEnc";

    /// <summary>
    /// Program data folder
    /// </summary>
    public const string ProgramData = "ProgramData";

    /// <summary>
    /// Sets the GC's "high memory load" threshold percent (clamped to 99 by the runtime). Parsed as
    /// <b>hexadecimal</b> by the runtime (see <c>GCToEEInterface::GetIntConfigValue</c>), and takes precedence
    /// over the <c>System.GC.HighMemoryPercent</c> runtimeconfig knob, which is parsed using C-style base
    /// detection (<c>0x</c>/<c>0X</c> prefix for hexadecimal, a leading <c>0</c> for octal, otherwise decimal -
    /// see <c>Configuration::GetKnobULONGLONGValue</c>).
    /// </summary>
    public const string DotNetGCHighMemPercent = "DOTNET_GCHighMemPercent";

    /// <summary>
    /// Legacy alias for <see cref="DotNetGCHighMemPercent"/>, also parsed as hexadecimal.
    /// </summary>
    public const string ComPlusGCHighMemPercent = "COMPlus_GCHighMemPercent";
}
