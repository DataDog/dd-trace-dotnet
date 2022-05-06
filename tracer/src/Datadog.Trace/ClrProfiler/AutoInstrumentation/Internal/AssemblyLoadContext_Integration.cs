// <copyright file="AssemblyLoadContext_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Internal
{
    /// <summary>
    /// System.Web.ThreadContext.DisassociateFromCurrentThread calltarget instrumentation
    /// </summary>
    /*
    [InstrumentMethod(
        AssemblyName = "System.Private.CoreLib",
        TypeName = "System.Runtime.Loader.AssemblyLoadContext",
        MethodName = "ResolveUsingEvent",
        ReturnTypeName = "System.Reflection.Assembly",
        ParameterTypeNames = new string[] { "System.Reflection.AssemblyName" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "*.*.*",
        IntegrationName = IntegrationName)]
    */
    [InstrumentMethod(
        AssemblyName = "System.Private.CoreLib",
        TypeName = "System.Runtime.Loader.AssemblyLoadContext",
        MethodName = "ResolveUsingLoad",
        ReturnTypeName = "System.Reflection.Assembly",
        ParameterTypeNames = new string[] { "System.Reflection.AssemblyName" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "*.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AssemblyLoadContext_Integration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.Internal);
        internal static readonly Assembly InstrumentationAssembly = Assembly.GetExecutingAssembly();
        internal static readonly AssemblyName InstrumentationAssemblyName = InstrumentationAssembly.GetName();
    }
}
#endif
