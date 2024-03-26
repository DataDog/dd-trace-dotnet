// <copyright file="RegistryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows
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

        public string? GetLocalMachineValue(string key)
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return null;
            }

            var registryKey = Registry.LocalMachine.OpenSubKey(key);

            return registryKey?.GetValue(null)?.ToString();
        }

        public string[] GetLocalMachineKeyNames(string key)
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

            return registryKey.GetSubKeyNames();
        }

        public string? GetLocalMachineKeyNameValue(string key, string subKeyName, string name)
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return string.Empty;
            }

            var registryKey = Registry.LocalMachine.OpenSubKey(key);

            if (registryKey == null)
            {
                return string.Empty;
            }

            var subKey = registryKey.OpenSubKey(subKeyName);

            return subKey?.GetValue(name)?.ToString();
        }
    }
}
