// <copyright file="DefaultModelBindingContext_SetResult_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System.Collections;
using System.ComponentModel;
using System.Data;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// setModel calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = "Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext",
    MethodName = "set_Result",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult"],
    MinimumVersion = "2.0.0.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
    [InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = "Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext",
    MethodName = "set_Result",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult" },
    MinimumVersion = "2.0.0.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DefaultModelBindingContext_SetResult_Integration
    {
        /// <summary>
        /// IntegrationName integration name
        /// </summary>
        private const string IntegrationName = nameof(IntegrationId.AspNetCore);

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception? exception, in CallTargetState state)
        {
            var iast = Iast.Iast.Instance;
            var security = Security.Instance;
            if (security.AppsecEnabled || iast.Settings.Enabled)
            {
                if (instance.TryDuckCast<DefaultModelBindingContext>(out var defaultModelBindingContext))
                {
                    if (defaultModelBindingContext.Result.IsModelSet && defaultModelBindingContext.IsTopLevelObject)
                    {
                        if ((state.Scope ?? state.PreviousScope)?.Span is not Span span)
                        {
                            return CallTargetReturn.GetDefault();
                        }

                        if (defaultModelBindingContext.BindingSource.Id == "Body")
                        {
                            CheckBody(defaultModelBindingContext, security, iast, span);
                        }
                        else if (defaultModelBindingContext.ValueProvider is IList valueProviderList)
                        {
                            for (var i = 0; i < valueProviderList.Count; i++)
                            {
                                var provider = valueProviderList[i];
                                if (provider.TryDuckCast(out BindingSourceValueProvider prov) && prov.BindingSource.Id is "Form" or "Body")
                                {
                                    CheckBody(defaultModelBindingContext, security, iast, span);
                                    break;
                                }
                            }
                        }
                        else if (defaultModelBindingContext.ValueProvider.TryDuckCast(out BindingSourceValueProvider prov) && prov.BindingSource.Id is "Form" or "Body")
                        {
                            CheckBody(defaultModelBindingContext, security, iast, span);
                        }
                    }
                }
            }

            return CallTargetReturn.GetDefault();
        }

        private static void CheckBody(DefaultModelBindingContext context, Security security, Iast.Iast iast, Datadog.Trace.Span span)
        {
            object? bodyExtracted = null;

            if (security.AppsecEnabled)
            {
                bodyExtracted = security.CheckBody(context.HttpContext, span, context.Result.Model, false);
            }

            if (iast.Settings.Enabled)
            {
                span.Context?.TraceContext?.IastRequestContext?.AddRequestBody(context.Result.Model, bodyExtracted);
            }
        }
    }
}
#endif
