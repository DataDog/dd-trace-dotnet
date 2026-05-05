// <copyright file="UserSettingsStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text.Json;

namespace ReferenceChainExplorer.Settings;

/// <summary>
/// Persists and loads <see cref="UserSettings"/> to the user's AppData folder.
/// </summary>
public static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Load settings from disk. Returns default settings if the file does not exist or cannot be read.
    /// </summary>
    public static UserSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    /// <summary>
    /// Save settings to disk.
    /// </summary>
    public static void Save(UserSettings settings)
    {
        if (settings is null)
        {
            return;
        }

        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ReferenceChainExplorer", "settings.json");
    }
}
