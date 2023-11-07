// <copyright file="MvcOptionsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// The ASP.NET Core middleware integration.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = TypeName,
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = Major3,
    MaximumVersion = Major7,
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class MvcOptionsIntegration
{
    private const string Major2 = "2";
    private const string Major3 = "3";
    private const string Major7 = "7";
    private const string TypeName = "Microsoft.AspNetCore.Mvc.MvcOptions";

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception exception, in CallTargetState state)
        where TTarget : IMvcOptions
    {
        instance.Filters.Add(new ActionResponseFilter());
        return CallTargetReturn.GetDefault();
    }
}
#endif
