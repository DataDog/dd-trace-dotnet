// <copyright file="AppProductChangePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

internal class AppProductChangePayload : IPayload
{
    public AppProductChangePayload(ProductsData products)
    {
        Products = products;
    }

    public ProductsData Products { get; set; }
}
