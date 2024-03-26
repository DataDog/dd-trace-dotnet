// <copyright file="FireOnStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// FireOnStartCommon integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.Kestrel.Core",
    TypeName = "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol",
    MethodName = "FireOnStarting",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "8.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.IIS",
    TypeName = "Microsoft.AspNetCore.Server.IIS.Core.IISHttpContext",
    MethodName = "FireOnStarting",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "8.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FireOnStartCommon
{
    private const string IntegrationName = nameof(IntegrationId.AspNetCore);

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TResponseContext">Type of the response</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="responseContext">Response context instance</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    public static CallTargetReturn<TResponseContext> OnMethodEnd<TTarget, TResponseContext>(TTarget instance, TResponseContext responseContext, Exception exception, in CallTargetState state)
    {
        var security = Security.Instance;
        var iastEnabled = Iast.Iast.Instance.Settings.Enabled;

        if (security.Enabled || iastEnabled)
        {
            var responseHeaders = instance.DuckCast<HttpProtocolStruct>().ResponseHeaders;
            var requestHeaders = instance.DuckCast<HttpProtocolStruct>().RequestHeaders;
            var span = Tracer.Instance.InternalActiveScope?.Root?.Span;

            if (responseHeaders is not null)
            {
                if (security.Enabled)
                {
                    if (span is not null)
                    {
                        SecurityCoordinatorHelpers.CheckReturnedHeaders(security, span, responseHeaders);
                    }
                }

                if (iastEnabled && IastModule.AddRequestVulnerabilitiesAllowed())
                {
                    if (span is not null)
                    {
                        var statusCode = instance.DuckCast<HttpProtocolStruct>().StatusCode;
                        var scheme = instance.DuckCast<HttpProtocolStruct>().Scheme;

                        ReturnedHeadersAnalyzer.Analyze(responseHeaders, IntegrationId.AspNetCore, span.ServiceName, statusCode, scheme);
                        InsecureAuthAnalyzer.AnalyzeInsecureAuth(requestHeaders, IntegrationId.AspNetCore, statusCode);
                    }

                    CookieAnalyzer.AnalyzeCookies(responseHeaders, IntegrationId.AspNetCore);
                }
            }
        }

        return new CallTargetReturn<TResponseContext>(responseContext);
    }

    [DuckCopy]
    internal struct HttpProtocolStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public IHeaderDictionary RequestHeaders;
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public IHeaderDictionary ResponseHeaders;
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public int StatusCode;
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public string Scheme;
    }
}

#endif
