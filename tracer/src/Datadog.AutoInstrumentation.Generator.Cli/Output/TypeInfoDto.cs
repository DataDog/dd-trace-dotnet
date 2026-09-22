// <copyright file="TypeInfoDto.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal class TypeInfoDto
{
    public string FullName { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsPublic { get; init; }

    public bool IsInterface { get; init; }

    public bool IsAbstract { get; init; }

    public bool IsSealed { get; init; }

    public bool IsValueType { get; init; }

    public int MethodCount { get; init; }

    public int NestedTypes { get; init; }
}
