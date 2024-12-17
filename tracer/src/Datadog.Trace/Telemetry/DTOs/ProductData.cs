// <copyright file="ProductData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Numerics;

namespace Datadog.Trace.Telemetry;

internal class ProductData
{
    public ProductData(bool enabled, ErrorData? error, BigInteger? state = null)
    {
        Enabled = enabled;
        Error = error;
        State = state;
    }

    public BigInteger? State { get; }

    public bool Enabled { get; }

    public ErrorData? Error { get; }
}
