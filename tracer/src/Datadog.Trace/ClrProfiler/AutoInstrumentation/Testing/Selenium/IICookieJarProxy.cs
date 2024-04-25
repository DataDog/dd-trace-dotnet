// <copyright file="IICookieJarProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.ICookieJar
/// </summary>
internal interface IICookieJarProxy : IDuckType
{
    /// <summary>
    /// Calls method: System.Void OpenQA.Selenium.ICookieJar::AddCookie(OpenQA.Selenium.Cookie)
    /// </summary>
    void AddCookie(object cookie);
}
