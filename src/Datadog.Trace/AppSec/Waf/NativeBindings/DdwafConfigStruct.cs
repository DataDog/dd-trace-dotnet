// <copyright file="DdwafConfigStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal struct DdwafConfigStruct
    {
        public ulong MaxArrayLength;

        public ulong MaxMapDepth;

        public int MaxTimeStore;
    }
}
