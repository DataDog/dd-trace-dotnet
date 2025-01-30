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
        return ModifyEnvironmentVariablesWithRetry(log, tracerValues, SetEnvVars);
    }

    public static bool RemoveAllEnvironmentVariables(ILogger log, TracerValues tracerValues)
    {
        log.WriteInfo("Removing app pool environment variables");
        return ModifyEnvironmentVariablesWithRetry(log, tracerValues, RemoveEnvVars);
    }

    private static bool ModifyEnvironmentVariablesWithRetry(
        ILogger log,
        TracerValues tracerValues,
        Action<ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars)
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

            if (ModifyEnvironmentVariables(log, tracerValues, updateEnvVars))
            {
                return true;
            }
        }

        log.WriteError($"Failed to update IIS after {attempt} attempts");
        return false;
    }

    private static bool ModifyEnvironmentVariables(
        ILogger log,
        TracerValues tracerValues,
        Action<ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars)
    {
        if (!SetEnvironmentVariables(log, tracerValues, updateEnvVars, out var appPoolsWeMustReenableRecycling))
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
        TracerValues tracerValues,
        Action<ConfigurationElementCollection, ReadOnlyDictionary<string, string>> updateEnvVars,
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
            log.WriteInfo($"Updating applicationPoolDefaults environment variables");
            updateEnvVars(applicationPoolDefaults.GetCollection("environmentVariables"), tracerValues.RequiredEnvVariables);

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
                        // already disallowed, which is what we want, so don't need to modify it now _or_ later
                    }
                    else
                    {
                        appPoolsWeMustReenableRecycling.Add(poolName);
                        appPoolElement.GetChildElement("recycling")["disallowRotationOnConfigChange"] = true;
                    }

                    // Set the pool-specific env variables
                    updateEnvVars(appPoolElement.GetCollection("environmentVariables"), tracerValues.RequiredEnvVariables);
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
                    && appPoolElement.GetAttributeValue("name") is string poolName
                    && appPoolsWhichNeedToAllowRecycling.Contains(poolName))
                {
                    log.WriteInfo($"Re-enabling rotation on config change for app pool '{poolName}'");

                    // disable recycling of the pool, so that we don't force a restart when we update the pool
                    // we can't distinguish between "not set" and "set to false", but we only really care about
                    // if it was set to true, as we don't want to accidentally revert that later.
                    appPoolElement.GetChildElement("recycling")["disallowRotationOnConfigChange"] = false;
                }
            }

            log.WriteInfo("Saving applicationHost.config");
            serverManager.CommitChanges();
            return true;
        }
        catch (Exception ex)
        {
            log.WriteError(ex, $"Error re-enabling application pool recycling");
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

    private static void SetEnvVars(ConfigurationElementCollection envVars, ReadOnlyDictionary<string, string> requiredVariables)
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

            envVarEle["value"] = envVarValue;
            // log.WriteInfo($"Updated environment variable {key} to {envVarValue}");
            remainingValues.Remove(key);
        }

        foreach (var kvp in remainingValues)
        {
            var addEle = envVars.CreateElement("add");
            addEle["name"] = kvp.Key;
            addEle["value"] = kvp.Value;
            envVars.Add(addEle);
        }
    }

    private static void RemoveEnvVars(ConfigurationElementCollection envVars, ReadOnlyDictionary<string, string> requiredVariables)
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
            envVarsToRemove.Add(envVarEle);
        }

        if (envVarsToRemove is null)
        {
            return;
        }

        foreach (var element in envVarsToRemove)
        {
            envVars.Remove(element);
        }
    }
}
