// <copyright file="AspNetCoreAvailabilityChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;

namespace Datadog.Trace;

internal static class AspNetCoreAvailabilityChecker
{
    /// <summary>
    /// The feature switch the Datadog.Trace.Aot targets set to false when a NativeAOT application doesn't have the ASP.NET Core
    /// assemblies (see ILLink.Substitutions.xml).
    /// </summary>
    internal const string FeatureSwitch = "Datadog.Trace.AspNetCore.IsSupported";

    private static readonly Lazy<bool> IsAvailable = new(CheckForRequiredTypes);

    /// <summary>
    /// Gets a value indicating whether the application can have ASP.NET Core: false in a NativeAOT application without its
    /// assemblies, where ILC then leaves out the code that uses them (instead of compiling it to throw).
    /// </summary>
    public static bool IsSupported => !AppContext.TryGetSwitch(FeatureSwitch, out var isSupported) || isSupported;

    private static bool CheckForRequiredTypes()
    {
        try
        {
            // Try to load the HttpContext type
            // Assumes that this is only called when we would expect the type to
            // already be loaded, for example inside an ASP.NET Core request.
            // As this is only checked _once_, it assumes the customer doesn't call
            // this API both _before_ ASP.NET Core types are loaded _and_ afterwards.
            var httpContextType = Type.GetType("Microsoft.AspNetCore.Http.HttpContext, Microsoft.AspNetCore.Http.Abstractions", throwOnError: false);
            return httpContextType is not null;
        }
        catch
        {
            // Problem loading the type, so assume we're not in aspnetcore
            return false;
        }
    }

    /// <summary>
    /// Should be called when ASP.NET Core is expected to be loaded,
    /// and checks if ASP.NET Core types are available
    /// </summary>
    public static bool IsAspNetCoreAvailable() => IsAvailable.Value;
}
#endif
