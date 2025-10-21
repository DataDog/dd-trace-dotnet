// <copyright file="PlatformKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.ConfigurationSources.Registry;

#pragma warning disable SA1649 // File name must match first type defined in file
/// <summary>
/// Configuration key for CORECLR_PROFILER_PATH
/// </summary>
internal readonly struct PlatformKeyCoreclrProfilerPath : IConfigKey
{
    public string GetKey() => PlatformKeys.DotNetCoreClrProfiler;
}

/// <summary>
/// Configuration key for COR_PROFILER_PATH
/// </summary>
internal readonly struct PlatformKeyCorProfilerPath : IConfigKey
{
    public string GetKey() => PlatformKeys.DotNetClrProfiler;
}

/// <summary>
/// Configuration key for AWS_LAMBDA_FUNCTION_NAME
/// </summary>
internal readonly struct PlatformKeyAwsLambdaFunctionName : IConfigKey
{
    public string GetKey() => PlatformKeys.Aws.FunctionName;
}

/// <summary>
/// Configuration key for WEBSITE_OWNER_NAME
/// </summary>
internal readonly struct PlatformKeyWebsiteOwnerName : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.WebsiteOwnerNameKey;
}

/// <summary>
/// Configuration key for WEBSITE_RESOURCE_GROUP
/// </summary>
internal readonly struct PlatformKeyWebsiteResourceGroup : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.ResourceGroupKey;
}

/// <summary>
/// Configuration key for WEBSITE_SITE_NAME
/// </summary>
internal readonly struct PlatformKeyWebsiteSiteName : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.SiteNameKey;
}

/// <summary>
/// Configuration key for WEBSITE_COUNTERS_CLR
/// </summary>
internal readonly struct PlatformKeyWebsiteCountersClr : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.CountersKey;
}

/// <summary>
/// Configuration key for COMPUTERNAME
/// </summary>
internal readonly struct PlatformKeyComputername : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.InstanceNameKey;
}

/// <summary>
/// Configuration key for WEBSITE_INSTANCE_ID
/// </summary>
internal readonly struct PlatformKeyWebsiteInstanceId : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.InstanceIdKey;
}

/// <summary>
/// Configuration key for WEBSITE_OS
/// </summary>
internal readonly struct PlatformKeyWebsiteOs : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.OperatingSystemKey;
}

/// <summary>
/// Configuration key for WEBSITE_SKU
/// </summary>
internal readonly struct PlatformKeyWebsiteSku : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureAppService.WebsiteSKU;
}

/// <summary>
/// Configuration key for FUNCTIONS_EXTENSION_VERSION
/// </summary>
internal readonly struct PlatformKeyFunctionsExtensionVersion : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureFunctions.FunctionsExtensionVersion;
}

/// <summary>
/// Configuration key for FUNCTIONS_WORKER_RUNTIME
/// </summary>
internal readonly struct PlatformKeyFunctionsWorkerRuntime : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureFunctions.FunctionsWorkerRuntime;
}

/// <summary>
/// Configuration key for FUNCTIONS_WORKER_RUNTIME_VERSION
/// </summary>
internal readonly struct PlatformKeyFunctionsWorkerRuntimeVersion : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureFunctions.FunctionsWorkerRuntimeVersion;
}

/// <summary>
/// Configuration key for FUNCTIONS_APPLICATION_DIRECTORY
/// </summary>
internal readonly struct PlatformKeyFunctionsApplicationDirectory : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureFunctions.FunctionsApplicationDirectory;
}

/// <summary>
/// Configuration key for FUNCTIONS_WORKER_DIRECTORY
/// </summary>
internal readonly struct PlatformKeyFunctionsWorkerDirectory : IConfigKey
{
    public string GetKey() => PlatformKeys.AzureFunctions.FunctionsWorkerDirectory;
}

/// <summary>
/// Configuration key for FUNCTION_NAME (deprecated GCP runtime)
/// </summary>
internal readonly struct PlatformKeyDepFunctionName : IConfigKey
{
    public string GetKey() => PlatformKeys.GCPFunction.DeprecatedFunctionNameKey;
}

/// <summary>
/// Configuration key for GCP_PROJECT (deprecated GCP runtime)
/// </summary>
internal readonly struct PlatformKeyDepGcpProject : IConfigKey
{
    public string GetKey() => PlatformKeys.GCPFunction.DeprecatedProjectKey;
}

/// <summary>
/// Configuration key for K_SERVICE (GCP runtime)
/// </summary>
internal readonly struct PlatformKeyFunctionNameKey : IConfigKey
{
    public string GetKey() => PlatformKeys.GCPFunction.FunctionNameKey;
}

/// <summary>
/// Configuration key for FUNCTION_TARGET (GCP runtime)
/// </summary>
internal readonly struct PlatformKeyFunctionTarget : IConfigKey
{
    public string GetKey() => PlatformKeys.GCPFunction.FunctionTargetKey;
}

#pragma warning restore SA1649

