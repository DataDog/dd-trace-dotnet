// <copyright file="BrowserTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Browser test tags
/// </summary>
internal static class BrowserTags
{
    /// <summary>
    /// Browser driver tag
    /// </summary>
    public const string BrowserDriver = "test.browser.driver";

    /// <summary>
    /// Browser driver version tag
    /// </summary>
    public const string BrowserDriverVersion = "test.browser.driver_version";

    /// <summary>
    /// Brwoser name tag
    /// </summary>
    public const string BrowserName = "test.browser.name";

    /// <summary>
    /// Browser version tag
    /// </summary>
    public const string BrowserVersion = "test.browser.version";

    /// <summary>
    /// Is RUM active tag
    /// </summary>
    public const string IsRumActive = "test.is_rum_active";
}
