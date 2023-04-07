// <copyright file="AsmRemoteConfigurationProducts.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal static class AsmRemoteConfigurationProducts
{
    public static AsmFeaturesProduct AsmFeaturesProduct { get; } = new();

    public static AsmDataProduct AsmDataProduct { get; } = new();

    public static AsmDdProduct AsmDdProduct { get; } = new();

    public static AsmProduct AsmProduct { get; } = new();

    public static IReadOnlyDictionary<string, AsmRemoteConfigurationProduct> GetAll => new ReadOnlyDictionary<string, AsmRemoteConfigurationProduct>(new Dictionary<string, AsmRemoteConfigurationProduct>
    {
        { RcmProducts.AsmFeatures, AsmFeaturesProduct },
        { RcmProducts.Asm, AsmProduct },
        { RcmProducts.AsmDd, AsmDdProduct },
        { RcmProducts.AsmData, AsmDataProduct }
    });
}
