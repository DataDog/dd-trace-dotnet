// <copyright file="GacNativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Datadog.Trace.Tools.Runner.Gac;

[ComVisible(false)]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal sealed partial class GacNativeMethods : IDisposable
{
    private const string NetFrameworkSubKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

    private IntPtr _libPointer;

    private GacNativeMethods(IntPtr libPointer)
    {
        _libPointer = libPointer;
    }

    public static GacNativeMethods Create()
    {
        var fusionFullPath = GetFusionFullPath();
        var lPointer = NativeLibrary.Load(fusionFullPath);

        if (lPointer == IntPtr.Zero)
        {
            throw new Exception($"Error loading fusion library.");
        }

        return new GacNativeMethods(lPointer);
    }

    public void Dispose()
    {
        var lPointer = _libPointer;
        if (lPointer == IntPtr.Zero)
        {
            return;
        }

        _libPointer = IntPtr.Zero;
        NativeLibrary.Free(lPointer);
    }

    private static string GetFusionFullPath()
    {
        string fusionFullPath;
        using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(NetFrameworkSubKey))
        {
            var installPath = ndpKey?.GetValue("InstallPath")?.ToString();
            if (installPath is null)
            {
                throw new Exception(".NET Framework `InstallPath` registry key cannot be found.");
            }

            fusionFullPath = Path.Combine(installPath, "fusion.dll");
        }

        if (!File.Exists(fusionFullPath))
        {
            throw new FileNotFoundException($"{fusionFullPath} cannot be found.");
        }

        return fusionFullPath;
    }

    private IntPtr GetPointer()
    {
        var lPointer = _libPointer;
        if (lPointer == IntPtr.Zero)
        {
            throw new Exception("Fusion library has not been loaded.");
        }

        return lPointer;
    }
}
