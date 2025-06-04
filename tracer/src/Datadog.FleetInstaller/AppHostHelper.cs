// <copyright file="AppHostHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Web.Administration;

namespace Datadog.FleetInstaller;

internal static class AppHostHelper
{
    public static bool SetAllEnvironmentVariables(ILogger log, TracerValues tracerValues)
    {
        log.WriteInfo("Setting app pool environment variables");
        return ModifyEnvironmentVariablesWithRetry(log, tracerValues.IisRequiredEnvVariables, SetEnvVars);
    }

    public static bool RemoveAllEnvironmentVariables(ILogger log)
    {
        log.WriteInfo("Removing app pool environment variables");
        // we don't need to know the exact tracer values, we just use the _keys_ in removeEnvVars
        var envVars = new TracerValues(string.Empty).IisRequiredEnvVariables;
        return ModifyEnvironmentVariablesWithRetry(log, envVars, RemoveEnvVars);
    }

    public static bool GetAppPoolEnvironmentVariable(ILogger log, string environmentVariable, out string? value)
    {
        using var serverManager = new ServerManager();
        var appPoolsSection = GetApplicationPoolsSection(log, serverManager);
        if (appPoolsSection is null)
        {
            value = null;
            return false;
        }

        var (applicationPoolDefaults, applicationPoolsCollection) = appPoolsSection.Value;

        // Check defaults
        log.WriteInfo($"Checking applicationPoolDefaults for environment variable: {environmentVariable}");
        if (TryGetEnvVar(applicationPoolDefaults.GetCollection("environmentVariables"), environmentVariable) is { } envValue)
        {
            value = envValue;
            return true;
        }

        foreach (var appPoolElement in applicationPoolsCollection)
        {
            if (string.Equals(appPoolElement.ElementTagName, "add", StringComparison.OrdinalIgnoreCase))
            {
                // An app pool element
                var poolName = appPoolElement.GetAttributeValue("name") as string;
                if (poolName is null)
                {
                    // poolName can never be null, if it is, weirdness is afoot, so bail out
                    log.WriteInfo("Found app pool element without a name, skipping");
                    continue;
                }

                log.WriteInfo($"Checking app pool '{poolName}' for environment variable: {environmentVariable}");
                if (TryGetEnvVar(appPoolElement.GetCollection("environmentVariables"), environmentVariable) is { } poolEnvValue)
                {
                    value = poolEnvValue;
                    return true;
                }
            }
        }

        log.WriteInfo($"{environmentVariable} variable not found in any app pools");
        value = null;
        return false;
    }

    private static bool ModifyEnvironmentVariablesWithRetry(
        ILogger log,
        ReadOnlyDictionary<string, string> envVars,
        Action<ILogger, ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars)
    {
        // If the IIS host config is being modified, this may fail
        // We retry multiple times, as the final update is atomic
        // We could consider adding backoff here, but it's not clear that it's necessary
        var attempt = 0;
        while (attempt < 3)
        {
            attempt++;
            if (attempt > 1)
            {
                log.WriteInfo($"Attempt {attempt} to update IIS failed, retrying.");
            }

            if (ModifyEnvironmentVariables(log, envVars, updateEnvVars))
            {
                return true;
            }
        }

        log.WriteError($"Failed to update IIS after {attempt} attempts");
        return false;
    }

    private static bool ModifyEnvironmentVariables(
        ILogger log,
        ReadOnlyDictionary<string, string> envVars,
        Action<ILogger, ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars)
    {
        if (!SetEnvironmentVariables(log, envVars, updateEnvVars, out var appPoolsWeMustReenableRecycling))
        {
            // If we failed to set the environment variables, we don't need to re-enable recycling
            // because by definition we can't have saved successfully
            return false;
        }

        // We do this separately, because we have to do all the work again no matter what we do
        return ReEnableRecycling(log, appPoolsWeMustReenableRecycling);
    }

    private static bool SetEnvironmentVariables(
        ILogger log,
        ReadOnlyDictionary<string, string> envVars,
        Action<ILogger, ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars,
        out HashSet<string> appPoolsWeMustReenableRecycling)
    {
        appPoolsWeMustReenableRecycling = [];

        try
        {
            using var serverManager = new ServerManager();
            appPoolsWeMustReenableRecycling = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var appPoolsSection = GetApplicationPoolsSection(log, serverManager);
            if (appPoolsSection is null)
            {
                return false;
            }

            var (applicationPoolDefaults, applicationPoolsCollection) = appPoolsSection.Value;

            // Update defaults
            log.WriteInfo("Updating applicationPoolDefaults environment variables");
            updateEnvVars(log, applicationPoolDefaults.GetCollection("environmentVariables"), envVars);

            // Update app pools
            foreach (var appPoolElement in applicationPoolsCollection)
            {
                if (string.Equals(appPoolElement.ElementTagName, "add", StringComparison.OrdinalIgnoreCase))
                {
                    // An app pool element
                    var poolName = appPoolElement.GetAttributeValue("name") as string;
                    if (poolName is null)
                    {
                        // poolName can never be null, if it is, weirdness is afoot, so bail out
                        log.WriteInfo("Found app pool element without a name, skipping");
                        continue;
                    }

                    log.WriteInfo($"Updating app pool '{poolName}' environment variables");

                    // disable recycling of the pool, so that we don't force a restart when we update the pool
                    // we can't distinguish between "not set" and "set to false", but we only really care about
                    // if it was set to "true", as we don't want to accidentally revert that later.
                    if (appPoolElement.GetChildElement("recycling")["disallowRotationOnConfigChange"] as bool? ?? false)
                    {
                        log.WriteInfo($"App pool '{poolName}' recycling.disallowRotationOnConfigChange already set to true, skipping");
                    }
                    else
                    {
                        log.WriteInfo($"Setting app pool '{poolName}' recycling.disallowRotationOnConfigChange=true");
                        appPoolsWeMustReenableRecycling.Add(poolName);
                        appPoolElement.GetChildElement("recycling")["disallowRotationOnConfigChange"] = true;
                    }

                    // Set the pool-specific env variables
                    updateEnvVars(log, appPoolElement.GetCollection("environmentVariables"), envVars);
                }
            }

            log.WriteInfo("Saving applicationHost.config");
            serverManager.CommitChanges();
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Error updating application pools");
            return false;
        }
    }

    private static bool ReEnableRecycling(ILogger log, HashSet<string> appPoolsWhichNeedToAllowRecycling)
    {
        try
        {
            using var serverManager = new ServerManager();

            var appPoolsSection = GetApplicationPoolsSection(log, serverManager);
            if (appPoolsSection is null)
            {
                return false;
            }

            // Set env variables
            foreach (var appPoolElement in appPoolsSection.Value.AppPools)
            {
                if (string.Equals(appPoolElement.ElementTagName, "add", StringComparison.OrdinalIgnoreCase)
                    && appPoolElement.GetAttributeValue("name") is string poolName)
                {
                    if (appPoolsWhichNeedToAllowRecycling.Contains(poolName))
                    {
                        log.WriteInfo($"Re-enabling rotation on config change for app pool '{poolName}'");
                        appPoolElement.GetChildElement("recycling")["disallowRotationOnConfigChange"] = false;
                    }
                    else
                    {
                        log.WriteInfo($"Skipping re-enabling rotation for app pool '{poolName}'");
                    }
                }
            }

            log.WriteInfo("Saving applicationHost.config");
            serverManager.CommitChanges();
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, "Error re-enabling application pool recycling");
            return false;
        }
    }

    private static (ConfigurationElement AppPoolDefaults, ConfigurationElementCollection AppPools)? GetApplicationPoolsSection(ILogger log, ServerManager serverManager)
    {
        var config = serverManager.GetApplicationHostConfiguration();
        if (config is null)
        {
            log.WriteError("Error fetching application host configuration");
            return null;
        }

        var appPoolSectionName = "system.applicationHost/applicationPools";
        var appPoolsSection = config.GetSection(appPoolSectionName);
        if (appPoolsSection is null)
        {
            log.WriteError($"Error fetching application pools: no section {appPoolSectionName} found");
            return null;
        }

        var applicationPoolDefaults = appPoolsSection.GetChildElement("applicationPoolDefaults");
        if (applicationPoolDefaults is null)
        {
            log.WriteError("Error fetching application pool defaults: applicationPoolDefaults returned null");
            return null;
        }

        var applicationPoolsCollection = appPoolsSection.GetCollection();
        if (applicationPoolsCollection is null)
        {
            log.WriteError("Error fetching application pool collection: applicationPools collection returned null");
            return null;
        }

        return (applicationPoolDefaults, applicationPoolsCollection);
    }

    private static void SetEnvVars(ILogger log, ConfigurationElementCollection envVars, ReadOnlyDictionary<string, string> requiredVariables)
    {
        // Try to find the value we need
        // Update all the values we need
        var remainingValues = new Dictionary<string, string>(requiredVariables);

        foreach (var envVarEle in envVars)
        {
            if (!string.Equals(envVarEle.ElementTagName, "add", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (envVarEle.GetAttributeValue("name") is not string key
                || !remainingValues.TryGetValue(key, out var envVarValue))
            {
                continue;
            }

            // Should we record the previous value (so it's available in the logs later if necessary?)
            // I opted not to, to avoid the risk of leaking anything sensitive. Currently there _shouldn't_
            // be anything sensitive in there, but there _could_ be theoretically.
            log.WriteInfo($"Found existing value for '{key}' - replacing value");
            envVarEle["value"] = envVarValue;
            remainingValues.Remove(key);
        }

        foreach (var kvp in remainingValues)
        {
            // Similarly, not showing the value we're setting here. It isn't a sensitive value now, but
            // that won't always be the case, so this avoids the risk. Obviously that's less useful
            // for troubleshooting unfortunately.
            log.WriteInfo($"Adding new variable '{kvp.Key}'");
            var addEle = envVars.CreateElement("add");
            addEle["name"] = kvp.Key;
            addEle["value"] = kvp.Value;
            envVars.Add(addEle);
        }
    }

    private static void RemoveEnvVars(ILogger log, ConfigurationElementCollection envVars, ReadOnlyDictionary<string, string> requiredVariables)
    {
        // Try to find the value we need
        // Update all the values we need
        List<ConfigurationElement>? envVarsToRemove = null;

        foreach (var envVarEle in envVars)
        {
            if (!string.Equals(envVarEle.ElementTagName, "add", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (envVarEle.GetAttributeValue("name") is not string key
                || !requiredVariables.ContainsKey(key))
            {
                continue;
            }

            envVarsToRemove ??= new();
            log.WriteInfo($"Adding variable '{key}' to removal list");
            envVarsToRemove.Add(envVarEle);
        }

        if (envVarsToRemove is null)
        {
            return;
        }

        log.WriteInfo($"Removing {envVarsToRemove.Count} variables");
        foreach (var element in envVarsToRemove)
        {
            envVars.Remove(element);
        }
    }

    private static string? TryGetEnvVar(ConfigurationElementCollection envVars, string variable)
    {
        foreach (var envVarEle in envVars)
        {
            if (!string.Equals(envVarEle.ElementTagName, "add", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (envVarEle.GetAttributeValue("name") is string key
             && key.Equals(variable, StringComparison.OrdinalIgnoreCase))
            {
                return envVarEle["value"] as string;
            }
        }

        return null;
    }
}
