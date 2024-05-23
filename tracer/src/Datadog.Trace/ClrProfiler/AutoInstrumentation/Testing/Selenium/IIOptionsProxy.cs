// <copyright file="IIOptionsProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.IOptions
/// </summary>
internal interface IIOptionsProxy : IDuckType
{
    /// <summary>
    /// Gets a value of OpenQA.Selenium.ICookieJar
    /// </summary>
    IICookieJarProxy Cookies { get; }
}
