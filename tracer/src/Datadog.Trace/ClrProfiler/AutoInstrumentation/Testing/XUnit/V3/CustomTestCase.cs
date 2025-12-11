// <copyright file="CustomTestCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

internal sealed class CustomTestCase : ITestCase
{
    public object? Instance => null;

    public Type Type => typeof(CustomTestCase);

    public string? DisplayName { get; set; }

    public Dictionary<string, List<string>?>? Traits { get; set; }

    public string UniqueID { get; set; } = string.Empty;

    public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return nameof(CustomTestCase);
    }
}
