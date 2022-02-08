// <copyright file="PProfSampleValueType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.PProf.Export
{
    internal struct PProfSampleValueType
    {
        public PProfSampleValueType(string type, string unit)
        {
            Type = type;
            Unit = unit;
        }

        public string Type { get; }

        public string Unit { get; }
    }
}