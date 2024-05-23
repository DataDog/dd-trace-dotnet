// <copyright file="HttpCommandExecutorOnSendingRemoteHttpRequestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// System.Void OpenQA.Selenium.Remote.HttpCommandExecutor::OnSendingRemoteHttpRequest(OpenQA.Selenium.Remote.SendingRemoteHttpRequestEventArgs) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "WebDriver",
    TypeName = "OpenQA.Selenium.Remote.HttpCommandExecutor",
    MethodName = "OnSendingRemoteHttpRequest",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["OpenQA.Selenium.Remote.SendingRemoteHttpRequestEventArgs"],
    MinimumVersion = "3.12.0",
    MaximumVersion = "4.*.*",
    IntegrationName = SeleniumCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class HttpCommandExecutorOnSendingRemoteHttpRequestIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TEventArgs>(TTarget instance, TEventArgs eventArgs)
    {
        // Remove all IPC http requests to the WebDriver server from tracing
        if (eventArgs.TryDuckCast<ISendingRemoteHttpRequestEventArgsV4Proxy>(out var v4EventArgs))
        {
            v4EventArgs.AddHeader(HttpHeaderNames.TracingEnabled, "false");
        }
        else if (eventArgs.TryDuckCast<ISendingRemoteHttpRequestEventArgsProxy>(out var v3EventArgs) &&
                 v3EventArgs.Request is HttpWebRequest request)
        {
            request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        return CallTargetState.GetDefault();
    }
}
