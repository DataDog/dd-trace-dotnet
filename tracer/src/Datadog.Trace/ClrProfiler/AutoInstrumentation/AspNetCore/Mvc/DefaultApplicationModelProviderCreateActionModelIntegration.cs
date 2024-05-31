// <copyright file="DefaultApplicationModelProviderCreateActionModelIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Mvc;

/// <summary>
/// Microsoft.AspNetCore.Mvc.ApplicationModels.ActionModel Microsoft.AspNetCore.Mvc.ApplicationModels.DefaultApplicationModelProvider::CreateActionModel(System.Reflection.TypeInfo,System.Reflection.MethodInfo) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Mvc.Core",
    TypeName = "Microsoft.AspNetCore.Mvc.ApplicationModels.DefaultApplicationModelProvider",
    MethodName = "CreateActionModel",
    ReturnTypeName = "Microsoft.AspNetCore.Mvc.ApplicationModels.ActionModel",
    ParameterTypeNames = ["System.Reflection.TypeInfo", "System.Reflection.MethodInfo"],
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "8.*.*.*.*",
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DefaultApplicationModelProviderCreateActionModelIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DefaultApplicationModelProviderCreateActionModelIntegration>();
    private static readonly string[] AllowedVerbs = ["GET", "POST"];

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        try
        {
            AnalyzeActionModel(returnValue);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to analyze or report Verb Tampering vulnerability");
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }

    private static void AnalyzeActionModel(object? returnValue)
    {
        if (returnValue == null) { return; }
        if (!returnValue.TryDuckCast<IActionModel>(out var actionModel)) { return; }

        var selectors = actionModel.Selectors;
        foreach (var item in selectors)
        {
            if (!item.TryDuckCast<ISelectorModel>(out var selectorModel)) { continue; }

            var actionConstraints = selectorModel.ActionConstraints;
            foreach (var actionConstraint in actionConstraints)
            {
                if (!actionConstraint.TryDuckCast<IHttpMethodActionConstraint>(out var httpMethodActionConstraint)) { continue; }

                var httpMethods = httpMethodActionConstraint.HttpMethods;
                foreach (var httpMethod in httpMethods)
                {
                    if (AllowedVerbs.Contains(httpMethod)) { continue; }

                    // Detected a Verb Tampering vulnerability
                    var methodNameWithArgs = actionModel.ActionMethod.ToString();
                    var declaringType = actionModel.ActionMethod.DeclaringType;
                    var methodInfo = $"The method {methodNameWithArgs} in {declaringType} is vulnerable to verb tampering with the HTTP verb: {httpMethod}";
                    IastModule.OnVerbTamperingVulnerability(methodInfo);
                }
            }
        }
    }
}

#endif
