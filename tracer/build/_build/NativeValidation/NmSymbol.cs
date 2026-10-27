// <copyright file="NmSymbol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace NativeValidation;

internal sealed class NmSymbol
{
    public NmSymbol(char type, string name, string versionedName, string versionName)
    {
        Type = type;
        Name = name;
        VersionedName = versionedName;
        VersionName = versionName;
    }

    public char Type { get; }

    public string Name { get; }

    public string VersionedName { get; }

    public string VersionName { get; }

    public bool IsUndefined => Type is 'U' or 'w' or 'v';
}
