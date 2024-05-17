// <copyright file="PlatformAssemblyResolverAssemblyResolverEventIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
#if NETCOREAPP
using System.Runtime.Loader;
#endif
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

/// <summary>
/// System.Void Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.PlatformAssemblyResolver::.ctor() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.TestPlatform.PlatformAbstractions",
    TypeName = "Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.PlatformAssemblyResolver",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PlatformAssemblyResolverAssemblyResolverEventIntegration
{
    private const string IntegrationName = "TestPlatformAssemblyResolver";

    private static readonly Assembly CiVisibilityAssembly = typeof(Ci.CIVisibility).Assembly;

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        // - VsTest contains its own Assembly resolver to load the test project assemblies form the project binary folder.
        // - Because we are autoinstrumenting vstest we inject the `Datadog.Trace.dll` to the vstest assemblies.
        // - The vstest assembly resolver doesn't check about versions so, if:
        //      a. The test project contains a reference to the Datadog.Trace nuget with a version X.
        //      b. And we are instrumenting vstest, adding a reference to the Datadog.Trace version Y of the autoinstrumentation.
        //      c. Their custom assembly resolver will be triggered for the `Datadog.Trace` version Y, and it will get
        //         resolved to version X (the one in the bin path of the test project).
        // This will cause a crash in the runtime.
        // To workaround this issue in the .ctor of their custom AssemblyResolver we add a resolving method before them
        // to check if the `Datadog.Trace` to load is the same as the autoinstrumentation version. If that is the case
        // we just pass this assembly, if not we bail out so their custom resolver can pick the request.
#if NETCOREAPP
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            if (name.FullName == CiVisibilityAssembly.FullName)
            {
                Common.Log.Debug("[PlatformAssemblyResolverAssemblyResolverEventIntegration] Assembly resolved successfully! [{Name}]", name.FullName);
                return CiVisibilityAssembly;
            }

            return null;
        };
#else
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (new AssemblyName(args.Name).FullName == CiVisibilityAssembly.FullName)
            {
                Common.Log.Debug("[PlatformAssemblyResolverAssemblyResolverEventIntegration] Assembly resolved successfully! [{Name}]", args.Name);
                return CiVisibilityAssembly;
            }

            return null;
        };
#endif

        return CallTargetState.GetDefault();
    }
}
