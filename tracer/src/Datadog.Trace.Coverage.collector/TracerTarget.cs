// <copyright file="TracerTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    internal enum TracerTarget
    {
        Net461,
        Netstandard20,
        Netcoreapp31
    }
}
