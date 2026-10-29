// <copyright file="MethodInfoDto.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal class MethodInfoDto
{
    public string Name { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string ReturnType { get; init; } = string.Empty;

    public bool IsPublic { get; init; }

    public bool IsStatic { get; init; }

    public bool IsVirtual { get; init; }

    public bool IsAsync { get; init; }

    public List<ParameterInfoDto> Parameters { get; init; } = [];

    public int OverloadIndex { get; init; }

    public int OverloadCount { get; init; }
}
