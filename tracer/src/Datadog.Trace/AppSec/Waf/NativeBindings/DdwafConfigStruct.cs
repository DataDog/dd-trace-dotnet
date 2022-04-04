// <copyright file="DdwafConfigStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal struct DdwafConfigStruct
    {
        public uint MaxContainerSize;

        public uint MaxContainerDepth;

        public uint MaxStringLength;

        public IntPtr KeyRegex;

        public IntPtr ValueRegex;
    }
}
