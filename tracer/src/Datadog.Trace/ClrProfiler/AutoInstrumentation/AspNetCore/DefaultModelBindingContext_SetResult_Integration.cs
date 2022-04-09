// <copyright file="DefaultModelBindingContext_SetResult_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AspNetCore
{
    /// <summary>
    /// setModel calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = "Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext",
    MethodName = "set_Result",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult" },
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "6.*.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
    [InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = "Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext",
    MethodName = "set_Result",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult" },
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "6.*.*.*.*",
    IntegrationName = IntegrationName,
    CallTargetIntegrationType = IntegrationType.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DefaultModelBindingContext_SetResult_Integration
    {
        /// <summary>
        /// IntegrationName integration name
        /// </summary>
        private const string IntegrationName = nameof(IntegrationId.AspNetCore);

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception exception, in CallTargetState state)
        {
            var security = AppSec.Security.Instance;
            if (security.Settings.Enabled)
            {
                if (instance.TryDuckCast<DefaultModelBindingContext>(out var defaultModelBindingContext))
                {
                    if (defaultModelBindingContext.Result.IsModelSet && defaultModelBindingContext.IsTopLevelObject)
                    {
                        var span = (state.Scope ?? state.PreviousScope).Span;

                        if (defaultModelBindingContext.BindingSource.Id == "Body")
                        {
                            security.InstrumentationGateway.RaiseBodyAvailable(defaultModelBindingContext.HttpContext, span, defaultModelBindingContext.Result.Model);
                        }
                        else
                        {
                            for (var i = 0; i < defaultModelBindingContext.ValueProvider.Count; i++)
                            {
                                var provider = defaultModelBindingContext.ValueProvider[i];
                                if (provider.TryDuckCast(out BindingSourceValueProvider prov))
                                {
                                    if (prov.BindingSource.Id == "Form" || prov.BindingSource.Id == "Body")
                                    {
                                        security.InstrumentationGateway.RaiseBodyAvailable(defaultModelBindingContext.HttpContext, span, defaultModelBindingContext.Result.Model);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
