// <copyright file="PW_RET_CODE.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal enum PW_RET_CODE
    {
        PW_ERR_INTERNAL = -6,
        PW_ERR_TIMEOUT = -5,
        PW_ERR_INVALID_CALL = -4,
        PW_ERR_INVALID_RULE = -3,
        PW_ERR_INVALID_FLOW = -2,
        PW_ERR_NORULE = -1,
        PW_GOOD = 0,
        PW_MONITOR = 1,
        PW_BLOCK = 2
    }
}
