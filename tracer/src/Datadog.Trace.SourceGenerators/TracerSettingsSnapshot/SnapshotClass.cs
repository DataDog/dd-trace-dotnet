// <copyright file="SnapshotClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.TracerSettingsSnapshot;

internal readonly record struct SnapshotClass
{
    public readonly string Namespace;
    public readonly string FullyQualifiedOriginalClassName;
    public readonly string SnapshotClassName;
    public readonly EquatableArray<SettableProperty> Properties;

    public SnapshotClass(string ns, string fullyQualifiedOriginalClassName, string snapshotClassName, EquatableArray<SettableProperty> properties)
    {
        Namespace = ns;
        FullyQualifiedOriginalClassName = fullyQualifiedOriginalClassName;
        SnapshotClassName = snapshotClassName;
        Properties = properties;
    }
}
