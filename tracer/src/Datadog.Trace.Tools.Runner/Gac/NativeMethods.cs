// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Datadog.Trace.Tools.Runner.Gac;

[ComVisible(false)]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal sealed class NativeMethods
{
    private const string NetFrameworkSubKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

    private delegate int CreateAssemblyCacheDelegate(out IAssemblyCache ppAsmCache, int reserved);

    internal static AssemblyCacheContainer CreateAssemblyCache()
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

        var libPointer = NativeLibrary.Load(fusionFullPath);
        var createAssemblyCachePointer = NativeLibrary.GetExport(libPointer, nameof(CreateAssemblyCache));
        var createAssemblyCache = Marshal.GetDelegateForFunctionPointer<CreateAssemblyCacheDelegate>(createAssemblyCachePointer);
        var hr = createAssemblyCache(out var ppAsmCache, 0);
        if (hr != 0)
        {
            NativeLibrary.Free(libPointer);
            throw new TargetInvocationException($"Error creating AssemblyCache. HRESULT = {hr}", null);
        }

        return new AssemblyCacheContainer(libPointer, ppAsmCache);
    }
}
