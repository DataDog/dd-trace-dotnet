// <copyright file="AsmRemoteConfigurationProducts.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.AppSec;

internal static class AsmRemoteConfigurationProducts
{
    public static AsmFeaturesProduct AsmFeaturesProduct { get; } = new();

    public static AsmDataProduct AsmDataProduct { get; } = new();

    public static AsmDDProduct AsmDDProduct { get; } = new();
}
