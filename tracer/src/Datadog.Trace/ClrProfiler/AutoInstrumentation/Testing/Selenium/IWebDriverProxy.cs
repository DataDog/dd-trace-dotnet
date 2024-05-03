// <copyright file="IWebDriverProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

/// <summary>
/// DuckTyping interface for OpenQA.Selenium.WebDriver
/// </summary>
internal interface IWebDriverProxy : IDuckType
{
    /// <summary>
    /// Gets the <see cref="SessionId"/> for the current session of this driver.
    /// </summary>
    object? SessionId { get; }

    /// <summary>
    /// Gets the capabilities of the current driver.
    /// </summary>
    ICapabilities Capabilities { get; }

    /// <summary>
    /// Calls method: System.Object OpenQA.Selenium.WebDriver::ExecuteScript(System.String,System.Object[])
    /// </summary>
    [Duck(ParameterTypeNames = ["System.String", "System.Object[]"])]
    object? ExecuteScript(string script, object[]? args);

    /// <summary>
    /// Calls method: OpenQA.Selenium.IOptions OpenQA.Selenium.WebDriver::Manage()
    /// </summary>
    IIOptionsProxy Manage();
}
