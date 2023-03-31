// <copyright file="AzureAppServiceHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tests.PlatformHelpers;

public static class AzureAppServiceHelper
{
    public static IConfigurationSource GetRequiredAasConfigurationValues(
        string subscriptionId,
        string deploymentId,
        string planResourceGroup,
        string siteResourceGroup,
        string ddTraceDebug = null,
        string functionsVersion = null,
        string functionsRuntime = null,
        string enableCustomTracing = null,
        string enableCustomMetrics = null,
        bool addContextKey = true)
    {
        var vars = Environment.GetEnvironmentVariables();

        if (vars.Contains(ConfigurationKeys.AzureAppService.InstanceNameKey))
        {
            // This is the COMPUTERNAME key which we'll remove for consistent testing
            vars.Remove(ConfigurationKeys.AzureAppService.InstanceNameKey);
        }

        if (vars.Contains(ConfigurationKeys.DebugEnabled))
        {
            vars.Remove(ConfigurationKeys.DebugEnabled);
        }

        if (!vars.Contains(ConfigurationKeys.ApiKey))
        {
            // This is a needed configuration for the AAS extension
            vars.Add(ConfigurationKeys.ApiKey, "1");
        }

        if (addContextKey)
        {
            vars.Add(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1");
        }

        vars.Add(ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey, $"{subscriptionId}+{planResourceGroup}-EastUSwebspace");
        vars.Add(ConfigurationKeys.AzureAppService.ResourceGroupKey, siteResourceGroup);
        vars.Add(ConfigurationKeys.AzureAppService.SiteNameKey, deploymentId);
        vars.Add(ConfigurationKeys.AzureAppService.OperatingSystemKey, "windows");
        vars.Add(ConfigurationKeys.AzureAppService.InstanceIdKey, "instance_id");
        vars.Add(ConfigurationKeys.AzureAppService.InstanceNameKey, "instance_name");
        vars.Add(ConfigurationKeys.DebugEnabled, ddTraceDebug);

        if (functionsVersion != null)
        {
            vars.Add(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, functionsVersion);
        }

        if (functionsRuntime != null)
        {
            vars.Add(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, functionsRuntime);
        }

        vars.Add(ConfigurationKeys.AzureAppService.AasEnableCustomTracing, enableCustomTracing ?? "false");
        vars.Add(ConfigurationKeys.AzureAppService.AasEnableCustomMetrics, enableCustomMetrics ?? "false");

        var collection = new NameValueCollection();

        foreach (DictionaryEntry kvp in vars)
        {
            collection.Add(kvp.Key as string, kvp.Value as string);
        }

        return new NameValueConfigurationSource(collection);
    }
}
