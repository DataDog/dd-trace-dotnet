// <copyright file="HtmlStringIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Iast;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Html
{
    /// <summary>
    /// System.Diagnostics.Process calltarget instrumentation
    /// </summary>
#if !NETFRAMEWORK
    [InstrumentMethod(
       AssemblyName = "Microsoft.AspNetCore.Html.Abstractions",
       TypeName = "Microsoft.AspNetCore.Html.HtmlString",
       MethodName = ".ctor",
       ParameterTypeNames = new[] { ClrNames.String },
       ReturnTypeName = ClrNames.Void,
       MinimumVersion = "1.0.0",
       MaximumVersion = "8.*.*",
       IntegrationName = nameof(Configuration.IntegrationId.Xss),
       InstrumentationCategory = InstrumentationCategory.Iast)]
#else
    [InstrumentMethod(
       AssemblyName = "System.Web",
       TypeName = "System.Web.HtmlString",
       MethodName = ".ctor",
       ParameterTypeNames = new[] { ClrNames.String },
       ReturnTypeName = ClrNames.Void,
       MinimumVersion = "4.0.0",
       MaximumVersion = "4.*.*",
       IntegrationName = nameof(Configuration.IntegrationId.Xss),
       InstrumentationCategory = InstrumentationCategory.Iast)]
#endif
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HtmlStringIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TArg1">Type of the argument 1 (System.String)</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="text">Instance of System.String</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 text)
        {
            IastModule.OnXss(text as string);

            return CallTargetState.GetDefault();
        }
    }
}
