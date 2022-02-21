// <copyright file="RegistryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

#nullable enable

namespace Datadog.Trace.Tools.Runner.Checks.Windows
{
    internal class RegistryService : IRegistryService
    {
        public string[] GetLocalMachineValueNames(string key)
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return Array.Empty<string>();
            }

            var registryKey = Registry.LocalMachine.OpenSubKey(key);

            if (registryKey == null)
            {
                return Array.Empty<string>();
            }

            return registryKey.GetValueNames();
        }

        public string? GetClsid(string key)
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return null;
            }

            var registryKey = Registry.ClassesRoot.OpenSubKey(key);

            return registryKey?.GetValue(null) as string;
        }
    }
}
