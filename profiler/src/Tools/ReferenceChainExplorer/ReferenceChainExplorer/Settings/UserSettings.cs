// <copyright file="UserSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainExplorer.Settings;

/// <summary>
/// User preferences persisted across application sessions.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Last folder used when loading a JSON file.
    /// </summary>
    public string? LastJsonFolder { get; set; }
}
