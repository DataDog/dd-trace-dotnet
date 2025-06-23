// <copyright file="GlobalEnvVariableHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.FleetInstaller;

internal static class GlobalEnvVariableHelper
{
    public static bool SetMachineEnvironmentVariables(
        ILogger log,
        TracerValues values,
        out Dictionary<string, string?> previousValues)
    {
        previousValues = new();
        try
        {
            log.WriteInfo("Installing global environment variables");

            foreach (var kvp in values.GlobalRequiredEnvVariables)
            {
                var previousValue = Environment.GetEnvironmentVariable(kvp.Key, EnvironmentVariableTarget.Machine);
                // We store the previous value, even if it's null,
                // so that we can revert (or remove) our replacements later if necessary
                previousValues[kvp.Key] = previousValue;

                log.WriteInfo($"Setting global environment variable {kvp.Key} to {kvp.Value}");
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Machine);
            }

            log.WriteInfo("All global environment variables installed");
            return true;
        }
        catch (Exception e)
        {
            log.WriteError("Failed to set global environment variables: " + e);
            return false;
        }
    }

    public static bool RevertMachineEnvironmentVariables(ILogger log, Dictionary<string, string?> previousValues)
    {
        try
        {
            foreach (var kvp in previousValues)
            {
                log.WriteInfo($"Reverting global environment variable {kvp.Key}");
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Machine);
            }

            log.WriteInfo("All global environment variables reverted");
            return true;
        }
        catch (Exception e)
        {
            log.WriteError("Failed to revert global environment variables: " + e);
            return false;
        }
    }

    public static bool RemoveMachineEnvironmentVariables(ILogger log)
    {
        try
        {
            log.WriteInfo("Removing global environment variables");

            // we don't need to know the exact tracer values, we just use the _keys_ in removeEnvVars
            var envVars = new TracerValues(string.Empty).GlobalRequiredEnvVariables;

            foreach (var envVar in envVars.Keys)
            {
                log.WriteInfo($"Removing global environment variable {envVar}");
                Environment.SetEnvironmentVariable(envVar, null, EnvironmentVariableTarget.Machine);
            }

            log.WriteInfo("All global environment variables removed");
            return true;
        }
        catch (Exception e)
        {
            log.WriteError($"Failed to remove global environment variables: {e}");
            return false;
        }
    }
}
