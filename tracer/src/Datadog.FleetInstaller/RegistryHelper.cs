// <copyright file="RegistryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Win32;

namespace Datadog.FleetInstaller;

internal class RegistryHelper
{
    public static bool AddCrashTrackingKey(ILogger log, TracerValues values, string registryKeyName)
        => AddOrUpdateValueForRegistryKey(log, registryKeyName, values.NativeLoaderX64Path, 1, RegistryValueKind.DWord, "crash tracking handler");

    public static bool RemoveCrashTrackingKey(ILogger log, TracerValues values, string registryKeyName)
        => RemoveValueForRegistryKey(log, registryKeyName, values.NativeLoaderX64Path, "crash tracking handler");

    public static bool TryGetIisVersion(ILogger log, [NotNullWhen(true)] out Version? version)
    {
        const string registryKeyName = @"Software\Microsoft\InetStp";

        log.WriteInfo($"Reading IIS information from registry key: '{registryKeyName}'");

        try
        {
            var key = Registry.LocalMachine.OpenSubKey(registryKeyName);
            if (key is null)
            {
                log.WriteInfo("IIS registry key not found");
                version = null;
                return false;
            }

            var major = key.GetValue("MajorVersion") as int? ?? 0;
            var minor = key.GetValue("MinorVersion") as int? ?? 0;

            version = new(major: major, minor: minor);

            log.WriteInfo($"Found IIS version: '{major}.{minor}'");
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Error reading the IIS Version from the registry key '{registryKeyName}'");
            version = null;
            return false;
        }
    }

    public static bool SetIisRegistrySettings(ILogger log, TracerValues values, string w3SvcKey, string wasKey)
    {
        var keyValues = values.IisRequiredEnvVariables.Select(kvp => kvp + "=" + kvp.Value).ToArray();
        if (!SetIisRegistrySettings(log, w3SvcKey, keyValues))
        {
            return false;
        }

        if (SetIisRegistrySettings(log, wasKey, keyValues))
        {
            return true;
        }

        // we assume we _didn't_ set the key for WAS seeing as that stage failed
        log.WriteError($"Rolling back IIS registry settings for {w3SvcKey}");
        RemoveIisRegistrySettings(log, w3SvcKey);
        return false;
    }

    public static bool RemoveIisRegistrySettings(ILogger log, string w3SvcKey, string wasKey)
    {
        // Always try to remove both keys, even if one fails
        var success1 = RemoveIisRegistrySettings(log, w3SvcKey);
        var success2 = RemoveIisRegistrySettings(log, wasKey);
        return success1 && success2;
    }

    private static bool SetIisRegistrySettings(ILogger log, string registryKeyName, string[] keyValues)
        => AddOrUpdateValueForRegistryKey(
            log,
            registryKeyName,
            valueName: "Environment",
            keyValues,
            RegistryValueKind.MultiString,
            "IIS fallback variables");

    private static bool RemoveIisRegistrySettings(ILogger log, string registryKeyName)
        => RemoveValueForRegistryKey(log, registryKeyName, valueName: "Environment", "IIS fallback variables");

    private static bool AddOrUpdateValueForRegistryKey(
        ILogger log,
        string registryKeyName,
        string valueName,
        object valueValue,
        RegistryValueKind valueKind,
        string description)
    {
        log.WriteInfo($"Adding {description} key to registry: '{registryKeyName}'");

        try
        {
            var key = Registry.LocalMachine.OpenSubKey(registryKeyName, writable: true);
            if (key is null)
            {
                log.WriteInfo($"Creating registry subkey: '{registryKeyName}'");

                key = Registry.LocalMachine.CreateSubKey(registryKeyName, writable: true);
                if (key is null)
                {
                    log.WriteError($"Registry key '{registryKeyName}' could not be created ");
                    return false;
                }
            }

            // This overwrites the key if it is already there
            key.SetValue(valueName, value: valueValue, valueKind);
            log.WriteInfo($"'{valueName}' added to {description} key '{registryKeyName}'");
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Failed to add {description} to registry key '{registryKeyName}'");
            return false;
        }
    }

    private static bool RemoveValueForRegistryKey(ILogger log, string registryKeyName, string valueName, string description)
    {
        log.WriteInfo($"Removing {description} from registry: '{registryKeyName}'");

        try
        {
            var key = Registry.LocalMachine.OpenSubKey(registryKeyName, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
            log.WriteInfo($"Value '{valueName}' for {description} removed from '{registryKeyName}'");

            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Failed to remove {description} from registry key '{registryKeyName}'");
            return false;
        }
    }
}
