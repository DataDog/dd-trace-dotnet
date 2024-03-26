// <copyright file="SettableProperty.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.TracerSettingsSnapshot;

internal readonly record struct SettableProperty
{
    public readonly string PropertyName;
    public readonly string ReturnType;
    public readonly string ConfigurationKey;

    public SettableProperty(string propertyName, string returnType, string configurationKey)
    {
        PropertyName = propertyName;
        ReturnType = returnType;
        ConfigurationKey = configurationKey;
    }
}
