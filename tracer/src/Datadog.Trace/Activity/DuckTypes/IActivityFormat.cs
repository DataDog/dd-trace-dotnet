// <copyright file="IActivityFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.DuckTypes
{
    // Using interface instead of [DuckCopy] struct as we need to set values too
    internal interface IActivityFormat
    {
        ActivityIdFormat DefaultIdFormat { get; set; }
    }
}
