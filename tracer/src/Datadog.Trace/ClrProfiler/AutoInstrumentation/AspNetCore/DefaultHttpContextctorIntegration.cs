// <copyright file="DefaultHttpContextctorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Wrappers;
using Microsoft.AspNetCore.Http.Features;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// System.Void Microsoft.AspNetCore.Http.DefaultHttpContext::.ctor(Microsoft.AspNetCore.Http.Features.IFeatureCollection) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Http",
    TypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.AspNetCore.Http.Features.IFeatureCollection"],
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "8.*.*.*",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DefaultHttpContextctorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TFeatures>(TTarget instance, ref TFeatures? features)
    {
        // Get the features from the collection: IHttpResponseFeature
        var responseFeature = ((IFeatureCollection?)features)?[typeof(IHttpResponseFeature)] as IHttpResponseFeature;
        if (responseFeature != null)
        {
            responseFeature.Headers = new HttpResponseHeadersWrapper(responseFeature.Headers);
        }

        return CallTargetState.GetDefault();
    }
}
#endif
