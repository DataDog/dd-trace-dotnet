// <copyright file="ISendingRemoteHttpRequestEventArgsProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.Remote.SendingRemoteHttpRequestEventArgs
/// </summary>
internal interface ISendingRemoteHttpRequestEventArgsProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.Net.HttpWebRequest
    /// </summary>
    object? Request { get; }
}

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.Remote.SendingRemoteHttpRequestEventArgs
/// </summary>
internal interface ISendingRemoteHttpRequestEventArgsV4Proxy : IDuckType
{
    /// <summary>
    /// Calls method: System.Void OpenQA.Selenium.Remote.SendingRemoteHttpRequestEventArgs::AddHeader(System.String,System.String)
    /// </summary>
    void AddHeader(string headerName, string headerValue);
}
