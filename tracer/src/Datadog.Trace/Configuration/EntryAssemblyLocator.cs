// <copyright file="EntryAssemblyLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Configuration;

internal static class EntryAssemblyLocator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EntryAssemblyLocator));

    /// <summary>
    /// Gets the entry assembly for the current application domain.
    /// </summary>
    /// <remarks>
    /// The timing in which you call this method is important. For IIS-based web applications, we rely on System.Web.HttpContext.Current
    /// to retrieve the entry assembly, which is only available during the request. Additionally, for OWIN-based web applications running
    /// on IIS, it's possible to call this method before the entry assembly is loaded.
    /// </remarks>
    internal static Assembly? GetEntryAssembly()
    {
        try
        {
            if (Assembly.GetEntryAssembly() is { } entryAssembly)
            {
                return entryAssembly;
            }

#if NETFRAMEWORK
            // If the entry assembly is null, we're probably running in a web environment.
            // Try and grab it off the HttpContext.
            try
            {
                if (GetEntryAssemblyFromHttpContext() is { } httpAssembly)
                {
                    return httpAssembly;
                }
            }
            catch (SecurityException)
            {
                // We don't have permission to access HttpContext.Current.
                // Nothing we can do about that unfortunately.
            }

            // In case of an OWIN app that is hosted in IIS using Microsoft.Owin.Host.SystemWeb, none of the previous techniques will work.
            // Therefore, we'll try to find the entry assembly by scanning through all loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsMicrosoftAssembly(assembly!) == false &&
                    assembly.CustomAttributes.Any(x => x.AttributeType.FullName == "Microsoft.Owin.OwinStartupAttribute"))
                {
                    return assembly;
                }
            }
#endif
        }
        catch (Exception e)
        {
            Log.Error(e, "Cannot find entry assembly");
        }

        return null;
    }

#if NETFRAMEWORK
    private static bool IsMicrosoftAssembly(Assembly assembly) => assembly.FullName?.StartsWith("Microsoft.") == true || assembly.FullName?.StartsWith("System.") == true;

    /// <summary>
    /// ! This method should be called from within a try-catch block !
    /// If the application is running in partial trust, then trying to call this method will result in
    /// a SecurityException being thrown at the method CALLSITE, not inside the <c>GetEntryAssemblyFromHttpContext(..)</c> method itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Assembly? GetEntryAssemblyFromHttpContext()
    {
        var type = System.Web.HttpContext.Current?.ApplicationInstance?.GetType();
        while (type is { Namespace: "ASP" })
        {
            type = type.BaseType;
        }

        if (type?.Assembly != null && !IsMicrosoftAssembly(type.Assembly))
        {
            return type.Assembly;
        }

        return null;
    }
#endif
}
