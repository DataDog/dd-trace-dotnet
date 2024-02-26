// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Coverage.Metadata;

namespace Datadog.Trace.Ci.Coverage;

internal class ModuleValue
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ModuleValue(ModuleCoverageMetadata metadata, Module module, int numberOfFiles)
    {
        Metadata = metadata;
        Module = module;
        Files = numberOfFiles == 0 ? [] : new FileValue[numberOfFiles];
    }

    public ModuleCoverageMetadata Metadata { get; }

    public Module Module { get; }

    public FileValue[] Files { get; }
}
