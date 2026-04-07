// <copyright file="ApplicationNameHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers;

internal static class ApplicationNameHelpers
{
    private const string UnknownServiceName = "UnknownService";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ApplicationNameHelpers));

    public static string GetFallbackApplicationName(TracerSettings settings)
        => GetApplicationName(settings) ?? UnknownServiceName;

    /// <summary>
    /// Gets a fallback "application name" for the executing application by looking at
    /// the hosted app name (.NET Framework on IIS only), assembly name, and process name.
    /// </summary>
    /// <returns>The default service name.</returns>
    private static string? GetApplicationName(TracerSettings settings)
    {
        try
        {
            if ((settings.IsRunningInAzureAppService || settings.IsRunningInAzureFunctions) &&
                settings.AzureAppServiceMetadata?.SiteName is { } siteName)
            {
                return siteName;
            }

            if (settings.LambdaMetadata is { IsRunningInLambda: true, ServiceName: var serviceName })
            {
                return serviceName;
            }

            try
            {
                if (TryLoadAspNetSiteName(out siteName))
                {
                    return siteName;
                }
            }
            catch (Exception ex)
            {
                // Unable to call into System.Web.dll
                Log.Error(ex, "Unable to get application name through ASP.NET settings");
            }

            return Assembly.GetEntryAssembly()?.GetName().Name ??
                   ProcessHelpers.GetCurrentProcessName();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating default service name");
            return null;
        }
    }

    private static bool TryLoadAspNetSiteName([NotNullWhen(true)] out string? siteName)
    {
#if NETFRAMEWORK
        try
        {
            // Call into a separate method to avoid TypeLoadException being thrown
            // during JIT compilation of this method when System.Web types are unavailable.
            // The NoInlining attribute ensures the JIT defers compilation until the call site.
            return TryGetHostingEnvironmentSiteName(out siteName);
        }
        catch (TypeLoadException ex)
        {
            Log.Warning(ex, "Unable to determine ASP.NET site name: HostingEnvironment type could not be loaded. This is expected when running ASP.NET Core on the .NET Framework CLR, which is not supported");
        }
#endif
        siteName = null;
        return false;
    }

#if NETFRAMEWORK
    /// <summary>
    /// This method must be called from within a try-catch block.
    /// The TypeLoadException will be thrown at the CALLSITE when System.Web types are unavailable,
    /// not inside this method, due to JIT compilation behavior.
    /// The NoInlining attribute is critical to ensure this behavior.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetHostingEnvironmentSiteName([NotNullWhen(true)] out string? siteName)
    {
        // System.Web.dll is only available on .NET Framework
        if (System.Web.Hosting.HostingEnvironment.IsHosted)
        {
            // if this app is an ASP.NET application, return "SiteName/ApplicationVirtualPath".
            // note that ApplicationVirtualPath includes a leading slash.
            siteName = (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
            return true;
        }

        siteName = null;
        return false;
    }
#endif
}
