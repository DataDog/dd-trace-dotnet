// <copyright file="ReturnCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf
{
    internal enum ReturnCode
    {
        ErrorInternal = -6,
        ErrorTimeout = -5,
        ErrorInvalidCall = -4,
        ErrorInvalidRule = -3,
        ErrorInvalidFlow = -2,
        ErrorNorule = -1,
        Good = 0,
        Monitor = 1,
        Block = 2
    }
}
