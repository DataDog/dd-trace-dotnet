// <copyright file="ICapabilities.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.ICapabilities
/// </summary>
internal interface ICapabilities
{
    /// <summary>
    /// Gets a capability of the browser.
    /// </summary>
    /// <param name="capability">The capability to get.</param>
    /// <returns>An object associated with the capability, or <see langword="null"/>
    /// if the capability is not set on the browser.</returns>
    object? GetCapability(string capability);
}
