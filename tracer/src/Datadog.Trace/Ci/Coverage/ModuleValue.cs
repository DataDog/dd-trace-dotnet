// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Coverage.Metadata;

namespace Datadog.Trace.Ci.Coverage;

internal class ModuleValue
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ModuleValue(ModuleCoverageMetadata metadata, Module module, int maxTypes)
    {
        Metadata = metadata;
        Module = module;
        Types = maxTypes == 0 ? Array.Empty<TypeValues>() : new TypeValues[maxTypes];
    }

    public ModuleCoverageMetadata Metadata { get; }

    public Module Module { get; }

    public TypeValues?[] Types { get; }
}
