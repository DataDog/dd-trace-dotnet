// <copyright file="RegistryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Microsoft.Win32;

namespace Datadog.FleetInstaller;

internal class RegistryHelper
{
    public static bool AddCrashTrackingKey(ILogger log, VersionedTracerValues values, string registryKeyName)
    {
        var crashHandlerPath = values.NativeLoaderX64Path;
        log.WriteInfo($"Adding crash tracking key to registry: '{registryKeyName}'");

        try
        {
            var key = Registry.LocalMachine.OpenSubKey(registryKeyName, writable: true);
            if (key is null)
            {
                log.WriteInfo($"Creating registry subkey: '{registryKeyName}'");

                key = Registry.LocalMachine.CreateSubKey(registryKeyName, writable: true);
                if (key is null)
                {
                    log.WriteError($"Registry key '{registryKeyName}' did not ");
                    return false;
                }
            }

            // This overwrites the key if it is already there
            key.SetValue(crashHandlerPath, value: 1, RegistryValueKind.DWord);
            log.WriteInfo($"Crash tracking handler path '{crashHandlerPath}' added to '{registryKeyName}'");
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Failed to add crash tracking handler to registry key '{registryKeyName}'");
            return false;
        }
    }

    public static bool RemoveCrashTrackingKey(ILogger log, VersionedTracerValues values, string registryKeyName)
    {
        var crashHandlerPath = values.NativeLoaderX64Path;
        log.WriteInfo($"Removing crash tracking key from registry: '{registryKeyName}'");

        try
        {
            var key = Registry.LocalMachine.OpenSubKey(registryKeyName, writable: true);
            key?.DeleteValue(crashHandlerPath);
            log.WriteInfo($"Crash tracking handler path '{crashHandlerPath}' removed from '{registryKeyName}'");

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Failed to remove crash tracking handler from registry key '{registryKeyName}'");
            return false;
        }
    }
}
