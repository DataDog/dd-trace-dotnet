// <copyright file="AzureAppServiceHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.TestHelpers.PlatformHelpers;

public static class AzureAppServiceHelper
{
    public static IConfigurationSource CreateMinimalAzureAppServiceConfiguration(string siteName)
    {
        var dict = new Dictionary<string, string>
        {
            { "WEBSITE_SITE_NAME", siteName }
        };

        return new DictionaryConfigurationSource(dict);
    }

    public static IConfigurationSource CreateMinimalAzureFunctionsConfiguration(string siteName, string functionsWorkerRuntime, string functionsExtensionVersion)
    {
        var dict = new Dictionary<string, string>
        {
            { "WEBSITE_SITE_NAME", siteName },
            { "FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated" },
            { "FUNCTIONS_EXTENSION_VERSION", "dotnet-isolated" }
        };

        return new DictionaryConfigurationSource(dict);
    }

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
            vars.Add(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, "3.0.0");
        }

        vars.Add(ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey, $"{subscriptionId}+{planResourceGroup}-EastUSwebspace");
        vars.Add(ConfigurationKeys.AzureAppService.ResourceGroupKey, siteResourceGroup);
        vars.Add(ConfigurationKeys.AzureAppService.SiteNameKey, deploymentId);
        vars.Add(ConfigurationKeys.AzureAppService.OperatingSystemKey, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux");
        vars.Add(ConfigurationKeys.AzureAppService.InstanceIdKey, "instance_id");
        vars.Add(ConfigurationKeys.AzureAppService.InstanceNameKey, "instance_name");
        vars.Add(ConfigurationKeys.DebugEnabled, ddTraceDebug);

        if (functionsVersion != null)
        {
            vars.Add(ConfigurationKeys.AzureFunctions.FunctionsExtensionVersion, functionsVersion);
        }

        if (functionsRuntime != null)
        {
            vars.Add(ConfigurationKeys.AzureFunctions.FunctionsWorkerRuntime, functionsRuntime);
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
