// <copyright file="ProbeTestDescription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.Helpers;

public class ProbeTestDescription : IXunitSerializable
{
    public bool IsOptimized { get; set; }

    public Type TestType { get; set; }

    public void Deserialize(IXunitSerializationInfo info)
    {
        IsOptimized = info.GetValue<bool>(nameof(IsOptimized));
        TestType = info.GetValue<Type>(nameof(TestType));
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(IsOptimized), IsOptimized);
        info.AddValue(nameof(TestType), TestType);
    }
}
