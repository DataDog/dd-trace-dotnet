// <copyright file="WafConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf
{
    internal class WafConstants
    {
        internal const int MaxStringLength = 4096;
        internal const int MaxObjectDepth = 15;
        internal const int MaxMapOrArrayLength = 256;
    }
}
