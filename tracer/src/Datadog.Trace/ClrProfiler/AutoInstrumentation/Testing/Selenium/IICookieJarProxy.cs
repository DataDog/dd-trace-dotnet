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

    /// <summary>
    /// Deletes the cookie with the specified name from the page.
    /// </summary>
    /// <param name="name">The name of the cookie to be deleted.</param>
    void DeleteCookieNamed(string name);

    /// <summary>
    /// Gets a cookie with the specified name.
    /// </summary>
    /// <param name="name">The name of the cookie to retrieve.</param>
    /// <returns>The Cookie containing the name. Returns <see langword="null"/>
    /// if no cookie with the specified name is found.</returns>
    object? GetCookieNamed(string name);
}
