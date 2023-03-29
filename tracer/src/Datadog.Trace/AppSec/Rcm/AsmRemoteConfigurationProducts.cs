// <copyright file="AsmRemoteConfigurationProducts.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Rcm;

internal static class AsmRemoteConfigurationProducts
{
    public static AsmFeaturesProduct AsmFeaturesProduct { get; } = new();

    public static AsmDataProduct AsmDataProduct { get; } = new();

    public static AsmDdProduct AsmDdProduct { get; } = new();

    public static AsmProduct AsmProduct { get; } = new();

    public static Dictionary<string, AsmRemoteConfigurationProduct> GetAll() => new()
    {
        { AsmFeaturesProduct.Name, AsmFeaturesProduct },
        { AsmProduct.Name, AsmProduct },
        { AsmDdProduct.Name, AsmDdProduct },
        { AsmDataProduct.Name, AsmDataProduct }
    };
}
