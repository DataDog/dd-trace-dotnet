// <copyright file="WafReturnCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf
{
    internal enum WafReturnCode
    {
        ErrorInternal = -3,
        ErrorInvalidObject = -2,
        ErrorInvalidArgument = -1,
        Ok = 0,
        Match = 1,
        Block = 2
    }
}
