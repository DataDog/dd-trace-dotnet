// <copyright file="DdwafVersionStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DdwafVersionStruct
    {
        public ushort Major;

        public ushort Minor;

        public ushort Patch;

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }
    }
}
